using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;
using Olbrasoft.VirtualAssistant.Data.Queries.LlmModelQueries;
using Olbrasoft.VirtualAssistant.Data.Queries.PromptQueries;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Abstract base class for LLM providers with shared functionality.
/// </summary>
public abstract class LlmProviderBase : ILlmProvider
{
    protected readonly HttpClient HttpClient;
    protected readonly ILogger Logger;
    protected readonly IPromptCache PromptCache;
    protected readonly IDesktopContextService DesktopContextService;
    protected readonly IQueryProcessor QueryProcessor;
    protected readonly ICliAppDetector CliAppDetector;
    private readonly IServiceScopeFactory _scopeFactory;

    private Dictionary<string, string> _lastRateLimitHeaders = new();
    private bool _runtimeEnabled;
    private int? _cachedModelId;

    /// <summary>
    /// Gets the provider name identifier (e.g., "zen", "mistral").
    /// </summary>
    public abstract string ProviderName { get; }

    /// <summary>
    /// Gets the model name being used.
    /// </summary>
    public abstract string ModelName { get; }

    /// <summary>
    /// Gets whether the provider is enabled in configuration.
    /// </summary>
    protected abstract bool ConfigEnabled { get; }

    /// <summary>
    /// Gets the minimum text length for correction.
    /// </summary>
    protected abstract int MinTextLength { get; }

    /// <summary>
    /// Gets the API endpoint path for chat completions.
    /// </summary>
    protected abstract string ChatCompletionsEndpoint { get; }

    /// <summary>
    /// Gets the provider-specific options.
    /// </summary>
    protected abstract ILlmProviderOptions Options { get; }

    protected LlmProviderBase(
        HttpClient httpClient,
        IPromptCache promptCache,
        ILogger logger,
        IDesktopContextService desktopContextService,
        IQueryProcessor queryProcessor,
        ICliAppDetector cliAppDetector,
        IServiceScopeFactory scopeFactory,
        bool initialEnabled)
    {
        HttpClient = httpClient;
        PromptCache = promptCache ?? throw new ArgumentNullException(nameof(promptCache));
        Logger = logger;
        DesktopContextService = desktopContextService ?? throw new ArgumentNullException(nameof(desktopContextService));
        QueryProcessor = queryProcessor ?? throw new ArgumentNullException(nameof(queryProcessor));
        CliAppDetector = cliAppDetector ?? throw new ArgumentNullException(nameof(cliAppDetector));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _runtimeEnabled = initialEnabled;
    }

    /// <summary>
    /// Gets the system prompt based on current desktop context.
    /// Priority: 1) CLI app detection (Claude Code, OpenCode), 2) Window title/app pattern, 3) Default prompt.
    /// Returns (promptText, promptId) tuple.
    /// </summary>
    protected async Task<(string PromptText, int PromptId)> GetSystemPromptAsync(CancellationToken ct)
    {
        try
        {
            var context = await DesktopContextService.GetCurrentContextAsync(ct);

            // Priority 1: Check for CLI apps running in terminals (e.g., Claude Code, OpenCode)
            // This handles cases where CLI apps run in terminal but don't change window title
            var cliApp = await CliAppDetector.DetectCliAppAsync(ct);
            if (cliApp != null)
            {
                Logger.LogDebug("CLI app detected: {AppName} → using prompt '{Prompt}'",
                    cliApp.AppName, cliApp.PromptFileName);

                // Get prompt ID from database by file name
                var cliPrompt = await QueryProcessor.ProcessAsync(
                    new GetPromptByFileNameQuery(cliApp.PromptFileName), ct);

                if (cliPrompt != null)
                {
                    var cliPromptText = PromptCache.GetPrompt(cliPrompt.PromptFileName);
                    Logger.LogDebug("Using prompt '{Prompt}' (ID: {Id}) for CLI app '{App}'",
                        cliPrompt.PromptFileName, cliPrompt.Id, cliApp.AppName);
                    return (cliPromptText, cliPrompt.Id);
                }

                // CLI app detected but no matching prompt in DB - use prompt file directly with ID 0
                Logger.LogWarning("CLI app '{App}' detected but prompt '{Prompt}' not found in database",
                    cliApp.AppName, cliApp.PromptFileName);
            }

            // Priority 2: Match by window title or application pattern
            Logger.LogDebug("Active window: '{Title}', app: '{App}', looking for matching prompt pattern",
                context.ActiveWindowTitle, context.ActiveApplication);

            var prompt = await QueryProcessor.ProcessAsync(
                new GetPromptByAppIdPatternQuery(context.ActiveWindowTitle, context.ActiveApplication), ct);

            // Priority 3: Default prompt
            prompt ??= await QueryProcessor.ProcessAsync(new GetDefaultPromptQuery(), ct);

            if (prompt == null)
            {
                Logger.LogError("CRITICAL: No default prompt found in database - using hardcoded fallback");
                return (PromptCache.GetPrompt("DefaultCorrection"), 4);
            }

            var promptText = PromptCache.GetPrompt(prompt.PromptFileName);

            Logger.LogDebug("Using prompt '{Prompt}' (ID: {Id}) for window '{Title}'",
                prompt.PromptFileName, prompt.Id, context.ActiveWindowTitle);

            return (promptText, prompt.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting context-aware prompt, falling back to DefaultCorrection (ID 4)");
            return (PromptCache.GetPrompt("DefaultCorrection"), 4);
        }
    }

    /// <summary>
    /// Gets the ModelId from database based on configured ModelIdentifier.
    /// Caches the result for subsequent calls.
    /// </summary>
    protected async Task<int?> GetModelIdAsync(CancellationToken ct)
    {
        if (_cachedModelId.HasValue)
            return _cachedModelId;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var queryProcessor = scope.ServiceProvider.GetRequiredService<IQueryProcessor>();

            var model = await queryProcessor.ProcessAsync(
                new GetLlmModelByIdentifierQuery(ModelName), ct);

            if (model != null)
            {
                _cachedModelId = model.Id;
                Logger.LogDebug("Resolved ModelId {ModelId} for model identifier '{ModelIdentifier}'",
                    model.Id, ModelName);
            }
            else
            {
                Logger.LogWarning("LlmModel with identifier '{ModelIdentifier}' not found in database. " +
                    "ModelId will be NULL in correction results.", ModelName);
            }

            return _cachedModelId;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error resolving ModelId for '{ModelIdentifier}'", ModelName);
            return null;
        }
    }

    /// <summary>
    /// Sets the runtime enabled state for LLM correction.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        _runtimeEnabled = enabled;
        Logger.LogInformation("{ProviderName} LLM correction {Status}", ProviderName, enabled ? "enabled" : "disabled");
    }

    /// <summary>
    /// Gets the current runtime enabled state.
    /// </summary>
    public bool IsEnabled() => _runtimeEnabled;

    /// <summary>
    /// Reloads all correction prompts by clearing the cache.
    /// </summary>
    public void ReloadPrompt()
    {
        PromptCache.ClearCache();
        Logger.LogInformation("{ProviderName} prompt cache cleared, prompts will reload on next request", ProviderName);
    }

    /// <summary>
    /// Gets the API usage information from the last response headers.
    /// </summary>
    public Dictionary<string, string> GetLastRateLimitHeaders()
    {
        return new Dictionary<string, string>(_lastRateLimitHeaders);
    }

    /// <summary>
    /// Captures rate limit headers from response.
    /// </summary>
    protected void CaptureRateLimitHeaders(HttpResponseMessage response)
    {
        _lastRateLimitHeaders = new Dictionary<string, string>();
        foreach (var header in response.Headers.Where(h => h.Key.StartsWith("x-ratelimit-", StringComparison.OrdinalIgnoreCase)))
        {
            _lastRateLimitHeaders[header.Key] = string.Join(", ", header.Value);
        }
    }

    /// <summary>
    /// Corrects the given text using the LLM provider. Resolves prompt internally.
    /// </summary>
    public abstract Task<LlmCorrectionResult> CorrectTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Corrects the given text using a pre-resolved prompt.
    /// Use this overload in racing mode to avoid concurrent DbContext access.
    /// </summary>
    public abstract Task<LlmCorrectionResult> CorrectTextAsync(string text, string promptText, int promptId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if correction should be skipped and returns the skip result if so.
    /// Returns null if correction should proceed.
    /// </summary>
    protected LlmCorrectionResult? CheckShouldSkip(string text)
    {
        if (!_runtimeEnabled)
        {
            Logger.LogDebug("Skipping LLM correction - {ProviderName} is disabled (runtime)", ProviderName);
            return new LlmCorrectionResult(text, null, 0);
        }

        if (!ConfigEnabled)
        {
            Logger.LogDebug("Skipping LLM correction - {ProviderName} is disabled (config)", ProviderName);
            return new LlmCorrectionResult(text, null, 0);
        }

        if (text.Length < MinTextLength)
        {
            Logger.LogDebug("Skipping LLM correction - text length {Length} < {MinLength}",
                text.Length, MinTextLength);
            return new LlmCorrectionResult(text, null, 0);
        }

        return null;
    }
}
