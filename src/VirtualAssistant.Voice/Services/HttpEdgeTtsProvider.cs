using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Configuration options for HttpEdgeTtsProvider.
/// </summary>
public sealed class HttpEdgeTtsOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "EdgeTtsServer";

    /// <summary>
    /// Base URL for EdgeTTS server (default: http://localhost:5555).
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:5555";
}

/// <summary>
/// TTS provider using local EdgeTTS HTTP server.
/// Calls the EdgeTTS WebSocket server running on localhost:5555.
/// </summary>
public sealed class HttpEdgeTtsProvider : ITtsProvider
{
    private readonly ILogger<HttpEdgeTtsProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly HttpEdgeTtsOptions _options;

    public HttpEdgeTtsProvider(
        ILogger<HttpEdgeTtsProvider> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<HttpEdgeTtsOptions> options)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("EdgeTtsServer");
        _options = options.Value;
    }

    /// <inheritdoc />
    public string Name => "EdgeTTS-HTTP";

    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <inheritdoc />
    public async Task<byte[]?> GenerateAudioAsync(string text, VoiceConfig config, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                text,
                voice = config.Voice,
                rate = config.Rate,
                volume = config.Volume,
                pitch = config.Pitch,
                returnAudio = true
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            var url = $"{_options.BaseUrl}/api/speech/speak";
            _logger.LogDebug("Calling EdgeTTS server at {Url}", url);

            var response = await _httpClient.PostAsync(url, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("EdgeTTS server returned {Status}: {Error}", response.StatusCode, error);
                return null;
            }

            // Check if response is audio (binary) or JSON
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType?.StartsWith("audio/") == true)
            {
                return await response.Content.ReadAsByteArrayAsync(cancellationToken);
            }

            // Server returned JSON (audio was played server-side)
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("EdgeTTS server response: {Response}", responseText);

            // Audio was played server-side, return empty (no need to play locally)
            return [];
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to connect to EdgeTTS server at {Url}", _options.BaseUrl);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating audio with EdgeTTS HTTP provider");
            return null;
        }
    }
}
