using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Data.Queries.PromptQueries;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Mistral AI provider for correcting Czech ASR transcriptions.
/// </summary>
public class MistralProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly MistralOptions _options;
    private readonly ILogger<MistralProvider> _logger;
    private readonly IPromptCache _promptCache;
    private readonly IDesktopContextService _desktopContextService;
    private readonly IQueryProcessor _queryProcessor;
    private Dictionary<string, string> _lastRateLimitHeaders = new();
    private bool _runtimeEnabled;

    /// <summary>
    /// Gets the provider name identifier ("mistral").
    /// </summary>
    public string ProviderName => "mistral";

    /// <summary>
    /// Gets the Mistral model name being used (e.g., "mistral-large-latest").
    /// </summary>
    public string ModelName => _options.Model;

    public MistralProvider(
        HttpClient httpClient,
        IOptions<MistralOptions> options,
        IPromptCache promptCache,
        ILogger<MistralProvider> logger,
        IDesktopContextService desktopContextService,
        IQueryProcessor queryProcessor)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _promptCache = promptCache ?? throw new ArgumentNullException(nameof(promptCache));
        _desktopContextService = desktopContextService ?? throw new ArgumentNullException(nameof(desktopContextService));
        _queryProcessor = queryProcessor ?? throw new ArgumentNullException(nameof(queryProcessor));

        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        _runtimeEnabled = _options.Enabled; // Initialize runtime state from config
    }

    /// <summary>
    /// Gets the system prompt based on current desktop context.
    /// Returns (promptText, promptId) tuple.
    /// Falls back to legacy MistralSystemPrompt on any error during context-aware prompt selection.
    /// </summary>
    private async Task<(string PromptText, int? PromptId)> GetSystemPromptAsync(CancellationToken ct)
    {
        try
        {
            // Get current desktop context
            var context = await _desktopContextService.GetCurrentContextAsync(ct);
            var activeApp = context.ActiveApplication; // e.g., "code", "ferdium", "chrome"

            _logger.LogDebug("Active application for prompt selection: '{App}'", activeApp);

            // Find appropriate prompt based on active application
            var prompt = await _queryProcessor.ProcessAsync(
                new GetPromptByAppIdPatternQuery(activeApp), ct);

            // Fallback to Default if no match
            prompt ??= await _queryProcessor.ProcessAsync(
                new GetDefaultPromptQuery(), ct);

            // Ensure we have a valid prompt (could be null if database is misconfigured)
            if (prompt == null)
            {
                _logger.LogWarning("No default prompt found in database, falling back to legacy MistralSystemPrompt");
                return (_promptCache.GetPrompt("MistralSystemPrompt"), null);
            }

            // Load prompt from cache (or filesystem/embedded)
            var promptText = _promptCache.GetPrompt(prompt.PromptFileName);

            _logger.LogDebug("Using prompt '{Prompt}' (ID: {Id}) for app '{App}'",
                prompt.PromptFileName, prompt.Id, activeApp);

            return (promptText, prompt.Id);
        }
        catch (InvalidOperationException ex)
        {
            // Expected exception when desktop monitoring is unavailable or prompt query fails
            _logger.LogWarning(ex, "Failed to get context-aware prompt, falling back to legacy MistralSystemPrompt");
            return (_promptCache.GetPrompt("MistralSystemPrompt"), null);
        }
        catch (Exception ex)
        {
            // Unexpected exception - log as error to surface potential bugs or configuration issues
            _logger.LogError(ex, "Unexpected error getting context-aware prompt, falling back to legacy MistralSystemPrompt");
            return (_promptCache.GetPrompt("MistralSystemPrompt"), null);
        }
    }

    /// <summary>
    /// Sets the runtime enabled state for LLM correction.
    /// This allows toggling LLM correction on/off at runtime without changing configuration.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        _runtimeEnabled = enabled;
        _logger.LogInformation("Mistral LLM correction {Status}", enabled ? "enabled" : "disabled");
    }

    /// <summary>
    /// Gets the current runtime enabled state.
    /// </summary>
    public bool IsEnabled() => _runtimeEnabled;

    /// <summary>
    /// Reloads all correction prompts by clearing the cache.
    /// Prompts will be reloaded from file (or embedded resource as fallback) on next API call.
    /// </summary>
    public void ReloadPrompt()
    {
        // Clear all cached prompts (avoids maintaining hardcoded list of prompt names)
        _promptCache.ClearCache();
        _logger.LogInformation("All Mistral prompt caches cleared, will reload on next request");
    }

    /// <summary>
    /// Corrects Czech ASR transcription using Mistral AI LLM with context-aware prompts.
    /// </summary>
    /// <param name="text">The transcribed text to correct.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>LLM correction result including corrected text, prompt ID, and duration.</returns>
    /// <exception cref="HttpRequestException">Thrown when HTTP request to Mistral API fails.</exception>
    /// <exception cref="TaskCanceledException">Thrown when request times out.</exception>
    /// <exception cref="InvalidOperationException">Thrown when API returns empty response.</exception>
    public async Task<LlmCorrectionResult> CorrectTextAsync(string text, CancellationToken cancellationToken = default)
    {
        // Skip LLM correction if disabled at runtime
        if (!_runtimeEnabled)
        {
            _logger.LogDebug("Skipping LLM correction - Mistral is disabled (runtime)");
            return new LlmCorrectionResult(text, null, 0);
        }

        // Skip LLM correction if disabled in configuration
        if (!_options.Enabled)
        {
            _logger.LogDebug("Skipping LLM correction - Mistral is disabled (config)");
            return new LlmCorrectionResult(text, null, 0);
        }

        // Skip LLM correction for short texts
        if (text.Length < _options.MinTextLengthForCorrection)
        {
            _logger.LogDebug("Skipping LLM correction - text length {Length} < {MinLength}",
                text.Length, _options.MinTextLengthForCorrection);
            return new LlmCorrectionResult(text, null, 0);
        }

        var startTime = DateTime.UtcNow;

        try
        {
            // Get context-aware prompt
            var (promptText, promptId) = await GetSystemPromptAsync(cancellationToken);

            var request = new
            {
                model = _options.Model,
                messages = new[]
                {
                    new { role = "system", content = promptText },  // Context-aware prompt
                    new { role = "user", content = text }
                },
                temperature = _options.Temperature,
                max_tokens = _options.MaxTokens
            };

            var response = await _httpClient.PostAsJsonAsync("/v1/chat/completions", request, cancellationToken);

            // Capture rate limit headers
            _lastRateLimitHeaders = new Dictionary<string, string>();
            foreach (var header in response.Headers.Where(h => h.Key.StartsWith("x-ratelimit-", StringComparison.OrdinalIgnoreCase)))
            {
                _lastRateLimitHeaders[header.Key] = string.Join(", ", header.Value);
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<MistralResponse>(cancellationToken);

            if (result?.Choices == null || result.Choices.Length == 0)
            {
                throw new InvalidOperationException("Mistral API returned empty response");
            }

            var correctedText = result.Choices[0].Message.Content.Trim();
            var durationMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            _logger.LogInformation("Mistral correction completed in {Duration}ms using prompt ID {PromptId}. Original length: {OriginalLength}, Corrected length: {CorrectedLength}",
                durationMs, promptId, text.Length, correctedText.Length);

            return new LlmCorrectionResult(correctedText, promptId, durationMs);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling Mistral API: {Message}", ex.Message);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Mistral API request timeout");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Mistral API: {Message}", ex.Message);
            throw;
        }
    }

    public Dictionary<string, string> GetLastRateLimitHeaders()
    {
        return new Dictionary<string, string>(_lastRateLimitHeaders);
    }

    private class MistralResponse
    {
        public MistralChoice[] Choices { get; set; } = Array.Empty<MistralChoice>();
    }

    private class MistralChoice
    {
        public MistralMessage Message { get; set; } = new();
    }

    private class MistralMessage
    {
        public string Content { get; set; } = string.Empty;
    }
}
