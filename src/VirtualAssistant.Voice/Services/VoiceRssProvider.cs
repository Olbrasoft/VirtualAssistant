using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// TTS provider using VoiceRSS API.
/// Czech male voice "Josef", requires API key (free tier: 350 requests/day).
/// </summary>
public sealed class VoiceRssProvider : ITtsProvider
{
    private const string ApiBaseUrl = "https://api.voicerss.org/";

    private readonly ILogger<VoiceRssProvider> _logger;
    private readonly HttpClient _httpClient;
    private readonly VoiceRssOptions _options;
    private readonly string? _apiKey;

    public VoiceRssProvider(
        ILogger<VoiceRssProvider> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<VoiceRssOptions> options)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("VoiceRSS");
        _options = options.Value;
        _apiKey = LoadApiKey();
    }

    /// <inheritdoc />
    public string Name => "VoiceRSS";

    /// <inheritdoc />
    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);

    /// <inheritdoc />
    public async Task<byte[]?> GenerateAudioAsync(string text, VoiceConfig config, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("VoiceRSS API key not configured");
            return null;
        }

        try
        {
            // Build query parameters
            var queryParams = new Dictionary<string, string>
            {
                ["key"] = _apiKey,
                ["hl"] = _options.Language,
                ["v"] = _options.Voice,
                ["src"] = text,
                ["c"] = _options.AudioCodec,
                ["f"] = _options.AudioFormat,
                ["r"] = _options.Speed.ToString()
            };

            var queryString = string.Join("&", queryParams.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

            var url = $"{ApiBaseUrl}?{queryString}";

            _logger.LogDebug("Calling VoiceRSS API for text: {Text}", text.Length > 50 ? text[..50] + "..." : text);

            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("VoiceRSS API returned {Status}: {Error}", response.StatusCode, error);
                return null;
            }

            // Check content type - VoiceRSS returns error messages as text/plain
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType == "text/plain")
            {
                var errorText = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("VoiceRSS API error: {Error}", errorText);
                return null;
            }

            var audioData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            _logger.LogDebug("VoiceRSS generated {Bytes} bytes of audio", audioData.Length);

            return audioData;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to connect to VoiceRSS API");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating audio with VoiceRSS");
            return null;
        }
    }

    private string? LoadApiKey()
    {
        try
        {
            var keyPath = _options.ApiKeyFile;

            // Expand ~ to home directory
            if (keyPath.StartsWith("~/"))
            {
                keyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), keyPath[2..]);
            }

            if (!File.Exists(keyPath))
            {
                _logger.LogWarning("VoiceRSS API key file not found: {Path}", keyPath);
                return null;
            }

            // Read file and extract API key
            var content = File.ReadAllText(keyPath);

            // Try to find API_KEY= line
            foreach (var line in content.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("API_KEY="))
                {
                    return trimmed["API_KEY=".Length..].Trim();
                }
            }

            // If no API_KEY= found, assume whole file is the key
            var key = content.Trim();
            if (key.Length == 32) // VoiceRSS keys are 32 chars
            {
                return key;
            }

            _logger.LogWarning("Could not parse API key from file: {Path}", keyPath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading VoiceRSS API key");
            return null;
        }
    }
}
