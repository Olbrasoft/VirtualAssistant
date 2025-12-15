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
            // play = false means server generates audio but doesn't play it - returns file path
            var request = new
            {
                text,
                voice = config.Voice,
                rate = config.Rate,
                volume = config.Volume,
                pitch = config.Pitch,
                play = false  // Don't play on server, we need the audio file
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

            // Parse JSON response to get audio file path
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("EdgeTTS server response: {Response}", responseText);

            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;

            // Check if generation was successful
            if (root.TryGetProperty("success", out var successProp) && !successProp.GetBoolean())
            {
                var message = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unknown error";
                _logger.LogWarning("EdgeTTS generation failed: {Message}", message);
                return null;
            }

            // Get the audio file path from response message
            // Format: "✅ Audio cached at: /path/to/file.mp3" or "✅ Audio generated at: /path/to/file.mp3"
            if (root.TryGetProperty("message", out var messageProp))
            {
                var message = messageProp.GetString() ?? "";
                var pathStart = message.IndexOf(": ", StringComparison.Ordinal);
                if (pathStart > 0)
                {
                    var audioPath = message[(pathStart + 2)..].Trim();
                    if (File.Exists(audioPath))
                    {
                        _logger.LogDebug("Reading audio from: {Path}", audioPath);
                        return await File.ReadAllBytesAsync(audioPath, cancellationToken);
                    }
                    _logger.LogWarning("Audio file not found: {Path}", audioPath);
                }
            }

            return null;
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
