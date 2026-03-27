using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Calls multiple LLM providers in parallel using Task.WhenAny and returns the first successful result.
/// If the first provider to complete fails, falls back to the remaining provider.
/// </summary>
public class RacingLlmProvider : IRacingLlmProvider
{
    private readonly ILlmProviderFactory _providerFactory;
    private readonly RacingOptions _options;
    private readonly ILogger<RacingLlmProvider> _logger;

    public RacingLlmProvider(
        ILlmProviderFactory providerFactory,
        IOptions<LlmProviderOptions> options,
        ILogger<RacingLlmProvider> logger)
    {
        _providerFactory = providerFactory;
        _options = options.Value.Racing;
        _logger = logger;
    }

    public async Task<RacingResult?> CorrectTextAsync(string text, CancellationToken cancellationToken = default)
    {
        var providers = ResolveProviders();

        if (providers.Count == 0)
        {
            _logger.LogWarning("No racing providers available");
            return null;
        }

        if (providers.Count == 1)
        {
            return await RunSingleProviderAsync(providers[0].Provider, providers[0].Name, text, cancellationToken);
        }

        if (providers.Count > 2)
        {
            _logger.LogWarning("Racing supports max 2 providers, using first 2 of {Count}: {Names}",
                providers.Count, string.Join(", ", providers.Select(p => p.Name)));
            providers = providers.Take(2).ToList();
        }

        return await RunRaceAsync(providers, text, cancellationToken);
    }

    private List<(ILlmProvider Provider, string Name)> ResolveProviders()
    {
        var result = new List<(ILlmProvider, string)>();

        foreach (var name in _options.Providers)
        {
            var provider = _providerFactory.GetProvider(name);
            if (provider != null && provider.IsEnabled())
            {
                result.Add((provider, name));
            }
            else
            {
                _logger.LogDebug("Racing provider '{Provider}' not available or disabled, skipping", name);
            }
        }

        return result;
    }

    private async Task<RacingResult?> RunSingleProviderAsync(
        ILlmProvider provider, string name, string text, CancellationToken cancellationToken)
    {
        try
        {
            var result = await provider.CorrectTextAsync(text, cancellationToken);
            var raceGroupId = Guid.NewGuid();
            _logger.LogInformation("Single-provider race: {Provider} completed in {Duration}ms", name, result.DurationMs);
            return new RacingResult(result, name, raceGroupId, LoserTask: null, LoserProviderName: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Single racing provider '{Provider}' failed", name);
            return null;
        }
    }

    private async Task<RacingResult?> RunRaceAsync(
        List<(ILlmProvider Provider, string Name)> providers, string text, CancellationToken cancellationToken)
    {
        var raceGroupId = Guid.NewGuid();

        // Start all providers concurrently
        var tasks = providers.Select(p => RunProviderAsync(p.Provider, p.Name, text, cancellationToken)).ToList();
        var taskToProvider = new Dictionary<Task<(LlmCorrectionResult? Result, string Name, Exception? Error)>, string>();
        for (int i = 0; i < tasks.Count; i++)
        {
            taskToProvider[tasks[i]] = providers[i].Name;
        }

        _logger.LogInformation("LLM race started: {Providers} (RaceGroupId: {RaceGroupId})",
            string.Join(" vs ", providers.Select(p => p.Name)), raceGroupId);

        // Wait for first to complete
        var remaining = new List<Task<(LlmCorrectionResult? Result, string Name, Exception? Error)>>(tasks);

        while (remaining.Count > 0)
        {
            var completed = await Task.WhenAny(remaining);
            remaining.Remove(completed);

            var (result, providerName, error) = await completed;

            if (error != null)
            {
                _logger.LogWarning(error, "Racing provider '{Provider}' failed, waiting for next", providerName);
                continue;
            }

            if (result == null)
            {
                _logger.LogWarning("Racing provider '{Provider}' returned null, waiting for next", providerName);
                continue;
            }

            // We have a winner!
            _logger.LogInformation("LLM race winner: {Winner} in {Duration}ms (RaceGroupId: {RaceGroupId})",
                providerName, result.DurationMs, raceGroupId);

            // Create loser task from remaining tasks
            Task<LlmCorrectionResult?>? loserTask = null;
            string? loserName = null;

            if (remaining.Count > 0)
            {
                var loserProviderTask = remaining[0];
                loserName = taskToProvider[loserProviderTask];
                loserTask = WrapLoserTaskAsync(loserProviderTask, loserName, raceGroupId);
            }

            return new RacingResult(result, providerName, raceGroupId, loserTask, loserName);
        }

        // All providers failed
        _logger.LogError("LLM race: all providers failed (RaceGroupId: {RaceGroupId})", raceGroupId);
        return null;
    }

    private static async Task<(LlmCorrectionResult? Result, string Name, Exception? Error)> RunProviderAsync(
        ILlmProvider provider, string name, string text, CancellationToken cancellationToken)
    {
        try
        {
            var result = await provider.CorrectTextAsync(text, cancellationToken);
            return (result, name, null);
        }
        catch (Exception ex)
        {
            return (null, name, ex);
        }
    }

    private async Task<LlmCorrectionResult?> WrapLoserTaskAsync(
        Task<(LlmCorrectionResult? Result, string Name, Exception? Error)> loserProviderTask,
        string loserName,
        Guid raceGroupId)
    {
        try
        {
            var (result, _, error) = await loserProviderTask;

            if (error != null)
            {
                _logger.LogWarning(error, "LLM race loser '{Provider}' failed (RaceGroupId: {RaceGroupId})",
                    loserName, raceGroupId);
                return null;
            }

            if (result != null)
            {
                _logger.LogInformation("LLM race loser '{Provider}' completed in {Duration}ms (RaceGroupId: {RaceGroupId})",
                    loserName, result.DurationMs, raceGroupId);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM race loser '{Provider}' threw exception (RaceGroupId: {RaceGroupId})",
                loserName, raceGroupId);
            return null;
        }
    }
}
