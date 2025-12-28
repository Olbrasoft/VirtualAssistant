using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        ILogger<MistralProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _promptCache = promptCache ?? throw new ArgumentNullException(nameof(promptCache));

        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        _runtimeEnabled = _options.Enabled; // Initialize runtime state from config
    }

    /// <summary>
    /// Gets the system prompt for Mistral from cache or loads it if not cached.
    /// </summary>
    private string GetSystemPrompt()
    {
        return _promptCache.GetPrompt("MistralSystemPrompt");
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
    /// Reloads the Mistral system prompt by clearing the cache.
    /// The prompt will be reloaded from file (or embedded resource as fallback) on next API call.
    /// </summary>
    public void ReloadPrompt()
    {
        _promptCache.ClearPrompt("MistralSystemPrompt");
        _logger.LogInformation("Mistral prompt cache cleared, will reload on next request");
    }

    /// <summary>
    /// Corrects Czech ASR transcription using Mistral AI LLM.
    /// </summary>
    /// <param name="text">The transcribed text to correct.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Corrected text, or original text if correction is skipped (disabled/short text).</returns>
    /// <exception cref="HttpRequestException">Thrown when HTTP request to Mistral API fails.</exception>
    /// <exception cref="TaskCanceledException">Thrown when request times out.</exception>
    /// <exception cref="InvalidOperationException">Thrown when API returns empty response.</exception>
    public async Task<string> CorrectTextAsync(string text, CancellationToken cancellationToken = default)
    {
        // Skip LLM correction if disabled at runtime
        if (!_runtimeEnabled)
        {
            _logger.LogDebug("Skipping LLM correction - Mistral is disabled (runtime)");
            return text;
        }

        // Skip LLM correction if disabled in configuration
        if (!_options.Enabled)
        {
            _logger.LogDebug("Skipping LLM correction - Mistral is disabled (config)");
            return text;
        }

        // Skip LLM correction for short texts
        if (text.Length < _options.MinTextLengthForCorrection)
        {
            _logger.LogDebug("Skipping LLM correction - text length {Length} < {MinLength}",
                text.Length, _options.MinTextLengthForCorrection);
            return text;
        }

        try
        {
            var request = new
            {
                model = _options.Model,
                messages = new[]
                {
                    new { role = "system", content = GetSystemPrompt() },
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

            _logger.LogInformation("Mistral correction completed. Original length: {OriginalLength}, Corrected length: {CorrectedLength}",
                text.Length, correctedText.Length);

            return correctedText;
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
