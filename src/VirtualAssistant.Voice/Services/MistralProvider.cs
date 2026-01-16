using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Data.Queries.LlmModelQueries;
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
    private int? _cachedModelId;

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
    /// Matches app_id_pattern against Active Window Title (e.g., "Claude Code", "Ferdium - WhatsApp", "OpenCode").
    /// Always returns valid prompt ID (never null) - defaults to ID 4 (DefaultCorrection) on any error.
    /// </summary>
    private async Task<(string PromptText, int PromptId)> GetSystemPromptAsync(CancellationToken ct)
    {
        try
        {
            // Get current desktop context
            var context = await _desktopContextService.GetCurrentContextAsync(ct);
            var windowTitle = context.ActiveWindowTitle; // e.g., "Claude Code", "Ferdium - WhatsApp", "OpenCode"

            _logger.LogDebug("Active window: '{Title}', looking for matching prompt pattern",
                windowTitle);

            // Find appropriate prompt based on window title (pattern matching against app_id_pattern)
            // Example: "Claude Code" matches pattern "code" → Programming Correction (ID 2)
            // Example: "OpenCode" matches pattern "opencode" → OpenCode Correction (ID 1)
            // Example: "Ferdium - WhatsApp" matches pattern "ferdium" → Ferdium Correction (ID 3)
            var prompt = await _queryProcessor.ProcessAsync(
                new GetPromptByAppIdPatternQuery(windowTitle), ct);

            // Fallback to Default if no match
            prompt ??= await _queryProcessor.ProcessAsync(
                new GetDefaultPromptQuery(), ct);

            // Ensure we have a valid prompt (should always work with GetDefaultPromptQuery)
            if (prompt == null)
            {
                _logger.LogError("CRITICAL: No default prompt found in database - using hardcoded fallback");
                return (_promptCache.GetPrompt("DefaultCorrection"), 4); // Hardcoded fallback to ID 4
            }

            // Load prompt from cache (or filesystem/embedded)
            var promptText = _promptCache.GetPrompt(prompt.PromptFileName);

            _logger.LogDebug("Using prompt '{Prompt}' (ID: {Id}) for window '{Title}'",
                prompt.PromptFileName, prompt.Id, windowTitle);

            return (promptText, prompt.Id);
        }
        catch (Exception ex)
        {
            // Any exception - log error and use default prompt with ID 4
            _logger.LogError(ex, "Error getting context-aware prompt, falling back to DefaultCorrection (ID 4)");
            return (_promptCache.GetPrompt("DefaultCorrection"), 4);
        }
    }

    /// <summary>
    /// Gets the ModelId from database based on configured ModelIdentifier.
    /// Caches the result for subsequent calls.
    /// </summary>
    private async Task<int?> GetModelIdAsync(CancellationToken ct)
    {
        if (_cachedModelId.HasValue)
            return _cachedModelId;

        try
        {
            var model = await _queryProcessor.ProcessAsync(
                new GetLlmModelByIdentifierQuery(_options.Model), ct);

            if (model != null)
            {
                _cachedModelId = model.Id;
                _logger.LogDebug("Resolved ModelId {ModelId} for model identifier '{ModelIdentifier}'",
                    model.Id, _options.Model);
            }
            else
            {
                _logger.LogWarning("LlmModel with identifier '{ModelIdentifier}' not found in database. " +
                    "ModelId will be NULL in correction results.", _options.Model);
            }

            return _cachedModelId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving ModelId for '{ModelIdentifier}'", _options.Model);
            return null;
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
            // Get context-aware prompt and model ID sequentially
            // (cannot run in parallel - DbContext is not thread-safe)
            var (promptText, promptId) = await GetSystemPromptAsync(cancellationToken);
            var modelId = await GetModelIdAsync(cancellationToken);

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

            _logger.LogInformation("Mistral correction completed in {Duration}ms using prompt ID {PromptId}, model ID {ModelId}. Original length: {OriginalLength}, Corrected length: {CorrectedLength}",
                durationMs, promptId, modelId, text.Length, correctedText.Length);

            return new LlmCorrectionResult(correctedText, promptId, durationMs, modelId);
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
