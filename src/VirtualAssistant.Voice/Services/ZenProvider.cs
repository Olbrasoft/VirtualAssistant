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
/// OpenCode Zen API provider for correcting Czech ASR transcriptions using alpha-glm-4.7 model.
/// </summary>
public class ZenProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly ZenOptions _options;
    private readonly ILogger<ZenProvider> _logger;
    private readonly IPromptCache _promptCache;
    private readonly IDesktopContextService _desktopContextService;
    private readonly IQueryProcessor _queryProcessor;
    private Dictionary<string, string> _lastRateLimitHeaders = new();
    private bool _runtimeEnabled;
    private int? _cachedModelId;

    /// <summary>
    /// Gets the provider name identifier ("zen").
    /// </summary>
    public string ProviderName => "zen";

    /// <summary>
    /// Gets the Zen model name being used (e.g., "alpha-glm-4.7").
    /// </summary>
    public string ModelName => _options.Model;

    public ZenProvider(
        HttpClient httpClient,
        IOptions<ZenOptions> options,
        IPromptCache promptCache,
        ILogger<ZenProvider> logger,
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

        _runtimeEnabled = _options.Enabled;
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
    /// Gets the system prompt based on current desktop context.
    /// Returns (promptText, promptId) tuple.
    /// </summary>
    private async Task<(string PromptText, int PromptId)> GetSystemPromptAsync(CancellationToken ct)
    {
        try
        {
            var context = await _desktopContextService.GetCurrentContextAsync(ct);
            var windowTitle = context.ActiveWindowTitle;

            _logger.LogDebug("Active window: '{Title}', looking for matching prompt pattern", windowTitle);

            var prompt = await _queryProcessor.ProcessAsync(
                new GetPromptByAppIdPatternQuery(windowTitle), ct);

            prompt ??= await _queryProcessor.ProcessAsync(new GetDefaultPromptQuery(), ct);

            if (prompt == null)
            {
                _logger.LogError("CRITICAL: No default prompt found in database - using hardcoded fallback");
                return (_promptCache.GetPrompt("DefaultCorrection"), 4);
            }

            var promptText = _promptCache.GetPrompt(prompt.PromptFileName);

            _logger.LogDebug("Using prompt '{Prompt}' (ID: {Id}) for window '{Title}'",
                prompt.PromptFileName, prompt.Id, windowTitle);

            return (promptText, prompt.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting context-aware prompt, falling back to DefaultCorrection (ID 4)");
            return (_promptCache.GetPrompt("DefaultCorrection"), 4);
        }
    }

    /// <summary>
    /// Sets the runtime enabled state for LLM correction.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        _runtimeEnabled = enabled;
        _logger.LogInformation("Zen LLM correction {Status}", enabled ? "enabled" : "disabled");
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
        _promptCache.ClearCache();
        _logger.LogInformation("Zen prompt cache cleared, prompts will reload on next request");
    }

    /// <summary>
    /// Corrects Czech ASR transcription using OpenCode Zen API with context-aware prompts.
    /// </summary>
    public async Task<LlmCorrectionResult> CorrectTextAsync(string text, CancellationToken cancellationToken = default)
    {
        // Skip LLM correction if disabled at runtime
        if (!_runtimeEnabled)
        {
            _logger.LogDebug("Skipping LLM correction - Zen is disabled (runtime)");
            return new LlmCorrectionResult(text, null, 0);
        }

        // Skip LLM correction if disabled in configuration
        if (!_options.Enabled)
        {
            _logger.LogDebug("Skipping LLM correction - Zen is disabled (config)");
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
            // Get context-aware prompt and model ID in parallel
            var promptTask = GetSystemPromptAsync(cancellationToken);
            var modelIdTask = GetModelIdAsync(cancellationToken);

            await Task.WhenAll(promptTask, modelIdTask);

            var (promptText, promptId) = promptTask.Result;
            var modelId = modelIdTask.Result;

            var request = new
            {
                model = _options.Model,
                messages = new[]
                {
                    new { role = "system", content = promptText },
                    new { role = "user", content = text }
                },
                temperature = _options.Temperature,
                max_tokens = _options.MaxTokens
            };

            var response = await _httpClient.PostAsJsonAsync("/chat/completions", request, cancellationToken);

            // Capture rate limit headers
            _lastRateLimitHeaders = new Dictionary<string, string>();
            foreach (var header in response.Headers.Where(h => h.Key.StartsWith("x-ratelimit-", StringComparison.OrdinalIgnoreCase)))
            {
                _lastRateLimitHeaders[header.Key] = string.Join(", ", header.Value);
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ZenResponse>(cancellationToken);

            if (result?.Choices == null || result.Choices.Length == 0)
            {
                throw new InvalidOperationException("Zen API returned empty response");
            }

            var correctedText = result.Choices[0].Message.Content.Trim();
            var durationMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            _logger.LogInformation("Zen correction completed in {Duration}ms using prompt ID {PromptId}, model ID {ModelId}. Original length: {OriginalLength}, Corrected length: {CorrectedLength}",
                durationMs, promptId, modelId, text.Length, correctedText.Length);

            return new LlmCorrectionResult(correctedText, promptId, durationMs, modelId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling Zen API: {Message}", ex.Message);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Zen API request timeout");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Zen API: {Message}", ex.Message);
            throw;
        }
    }

    public Dictionary<string, string> GetLastRateLimitHeaders()
    {
        return new Dictionary<string, string>(_lastRateLimitHeaders);
    }

    private class ZenResponse
    {
        public ZenChoice[] Choices { get; set; } = Array.Empty<ZenChoice>();
    }

    private class ZenChoice
    {
        public ZenMessage Message { get; set; } = new();
    }

    private class ZenMessage
    {
        public string Content { get; set; } = string.Empty;
    }
}
