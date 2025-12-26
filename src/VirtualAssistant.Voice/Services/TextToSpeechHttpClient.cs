using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// HTTP client for TextToSpeech.Service API.
/// Implements ITtsProviderChain interface but delegates to centralized TTS service.
/// </summary>
public sealed class TextToSpeechHttpClient : ITtsProviderChain
{
    private readonly ILogger<TextToSpeechHttpClient> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _serviceUrl;

    public TextToSpeechHttpClient(
        ILogger<TextToSpeechHttpClient> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _serviceUrl = "http://localhost:5060";
    }

    public async Task<(byte[]? Audio, string? ProviderUsed)> SynthesizeAsync(
        string text,
        VoiceConfig config,
        string? sourceProfile = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Parse rate and pitch from string to int (API expects -100 to +100)
            var rate = ParseRate(config.Rate) ?? 0;
            var pitch = ParsePitch(config.Pitch) ?? 0;

            var request = new
            {
                text,
                voice = string.IsNullOrEmpty(config.Voice) ? "cs-CZ-AntoninNeural" : config.Voice,
                rate,
                pitch
            };

            _logger.LogDebug("Calling TextToSpeech.Service API: {Text}",
                text.Length > 50 ? text[..50] + "..." : text);

            var response = await httpClient.PostAsJsonAsync(
                $"{_serviceUrl}/api/tts/synthesize",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("TextToSpeech.Service API error: {StatusCode} - {Error}",
                    response.StatusCode, errorContent);
                return (null, null);
            }

            // Check Content-Type to determine response format
            var contentType = response.Content.Headers.ContentType?.MediaType;

            if (contentType == "application/json")
            {
                // Error response
                var errorResponse = await response.Content.ReadFromJsonAsync<SynthesizeErrorResponse>(cancellationToken);
                _logger.LogError("TextToSpeech.Service synthesis failed: {Error}", errorResponse?.ErrorMessage);
                return (null, null);
            }

            // Success - audio bytes (audio/mpeg)
            var audioData = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            _logger.LogDebug("Received {Size} bytes of audio from TextToSpeech.Service", audioData.Length);

            // Extract provider from response headers if available
            string? providerUsed = null;
            if (response.Headers.TryGetValues("X-Provider-Used", out var values))
            {
                providerUsed = values.FirstOrDefault();
            }

            return (audioData, providerUsed ?? "TextToSpeech.Service");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to connect to TextToSpeech.Service at {Url}", _serviceUrl);
            return (null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling TextToSpeech.Service API");
            return (null, null);
        }
    }

    public IReadOnlyList<TtsProviderStatus> GetProvidersStatus()
    {
        // Return single provider status (TextToSpeech.Service itself)
        return new List<TtsProviderStatus>
        {
            new TtsProviderStatus(
                Name: "TextToSpeech.Service",
                IsHealthy: true, // Could check health endpoint
                LastFailure: null,
                NextRetryAt: null,
                ConsecutiveFailures: 0
            )
        };
    }

    public void ResetCircuitBreaker(string? providerName = null)
    {
        // No-op - circuit breaker is managed by TextToSpeech.Service
        _logger.LogDebug("Circuit breaker reset requested (delegated to TextToSpeech.Service)");
    }

    /// <summary>
    /// Parses rate string (e.g., "+10%", "-20%", "default") to integer percentage.
    /// </summary>
    private static int? ParseRate(string? rate)
    {
        if (string.IsNullOrEmpty(rate) || rate == "default")
            return null;

        // Remove % and parse
        var normalized = rate.Replace("%", "").Replace("+", "");
        if (int.TryParse(normalized, out var value))
            return value;

        return null;
    }

    /// <summary>
    /// Parses pitch string (e.g., "+5Hz", "-10Hz", "default") to integer.
    /// </summary>
    private static int? ParsePitch(string? pitch)
    {
        if (string.IsNullOrEmpty(pitch) || pitch == "default")
            return null;

        // Remove Hz and parse
        var normalized = pitch.Replace("Hz", "").Replace("+", "");
        if (int.TryParse(normalized, out var value))
            return value;

        return null;
    }

    private sealed record SynthesizeErrorResponse(
        bool Success,
        string? ErrorMessage,
        string? ProviderUsed
    );
}
