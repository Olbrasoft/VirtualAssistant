using System.Diagnostics;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Text-to-Speech service using direct WebSocket to Microsoft Edge TTS API.
/// Pure C# - no external dependencies like Python edge-tts.
/// </summary>
public sealed class TtsService : IDisposable
{
    private const string WSS_URL = "wss://api.msedgeservices.com/tts/cognitiveservices/websocket/v1";
    private const string API_KEY = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    private const string CHROMIUM_FULL_VERSION = "140.0.3485.14";
    private const long WIN_EPOCH = 11644473600;
    private const double S_TO_NS = 1e9;

    private readonly ILogger<TtsService> _logger;
    private readonly string _cacheDirectory;
    private readonly string _micLockFile = "/tmp/microphone-active.lock";
    private readonly string _voice = "cs-CZ-AntoninNeural";
    private readonly string _rate = "+20%";
    private readonly string _volume = "+0%";
    private readonly string _pitch = "+0Hz";

    public TtsService(ILogger<TtsService> logger)
    {
        _logger = logger;
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache", "virtual-assistant-tts");

        Directory.CreateDirectory(_cacheDirectory);
    }

    /// <summary>
    /// Řekne text přes Microsoft Edge TTS API (přímé WebSocket volání).
    /// </summary>
    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if microphone is active
            if (File.Exists(_micLockFile))
            {
                _logger.LogInformation("🎤 Mikrofon aktivní - text odložen: {Text}", text);
                return;
            }

            // Check cache first
            var cacheFile = GetCacheFilePath(text);
            if (File.Exists(cacheFile))
            {
                _logger.LogDebug("Playing from cache: {Text}", text);
                await PlayAudioAsync(cacheFile, cancellationToken);
                return;
            }

            // Generate new audio via WebSocket
            _logger.LogDebug("Generating audio for: {Text}", text);
            var audioData = await GenerateAudioAsync(text, cancellationToken);

            if (audioData == null || audioData.Length == 0)
            {
                _logger.LogWarning("Failed to generate audio for: {Text}", text);
                return;
            }

            // Save to cache
            await File.WriteAllBytesAsync(cacheFile, audioData, cancellationToken);

            // Play audio
            await PlayAudioAsync(cacheFile, cancellationToken);

            _logger.LogInformation("🗣️ TTS: \"{Text}\"", text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TTS error for text: {Text}", text);
        }
    }

    private async Task<byte[]?> GenerateAudioAsync(string text, CancellationToken cancellationToken)
    {
        using var client = new ClientWebSocket();

        // Add all required headers to match Microsoft Edge TTS requirements
        client.Options.SetRequestHeader("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36 Edg/140.0.0.0");
        client.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
        client.Options.SetRequestHeader("Pragma", "no-cache");
        client.Options.SetRequestHeader("Cache-Control", "no-cache");
        client.Options.SetRequestHeader("Accept-Encoding", "gzip, deflate, br");
        client.Options.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");

        // CRITICAL: Add WebSocket subprotocol
        client.Options.AddSubProtocol("synthesize");

        // Generate connection parameters
        var connectionId = Guid.NewGuid().ToString("N");
        var secMsGec = GenerateSecMsGec();
        var secMsGecVersion = $"1-{CHROMIUM_FULL_VERSION}";

        var uri = new Uri($"{WSS_URL}?Ocp-Apim-Subscription-Key={API_KEY}&ConnectionId={connectionId}&Sec-MS-GEC={secMsGec}&Sec-MS-GEC-Version={secMsGecVersion}");

        try
        {
            await client.ConnectAsync(uri, cancellationToken);

            // Send config message
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
            var configMessage = $"X-Timestamp:{timestamp}\r\n" +
                               "Content-Type:application/json; charset=utf-8\r\n" +
                               "Path:speech.config\r\n\r\n" +
                               "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{" +
                               "\"sentenceBoundaryEnabled\":\"false\",\"wordBoundaryEnabled\":\"false\"}," +
                               "\"outputFormat\":\"audio-24khz-48kbitrate-mono-mp3\"}}}}";

            await client.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(configMessage)),
                WebSocketMessageType.Text,
                true,
                cancellationToken);

            // Send SSML message
            var ssml = GenerateSsml(text);
            var requestId = Guid.NewGuid().ToString("N");
            var ssmlMessage = $"X-RequestId:{requestId}\r\n" +
                             "Content-Type:application/ssml+xml\r\n" +
                             "Path:ssml\r\n\r\n" +
                             ssml;

            await client.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(ssmlMessage)),
                WebSocketMessageType.Text,
                true,
                cancellationToken);

            // Receive audio data
            var audioChunks = new List<byte>();
            var buffer = new byte[16384];

            while (client.State == WebSocketState.Open)
            {
                var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // First 2 bytes = header length (big-endian)
                    if (result.Count < 2) continue;

                    var headerLength = (buffer[0] << 8) | buffer[1];
                    var audioStart = 2 + headerLength;

                    if (audioStart > result.Count) continue;

                    // Check if this is audio data
                    var headerBytes = buffer.Skip(2).Take(headerLength).ToArray();
                    var headerText = Encoding.UTF8.GetString(headerBytes);

                    if (headerText.Contains("Path:audio"))
                    {
                        audioChunks.AddRange(buffer.Skip(audioStart).Take(result.Count - audioStart));
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    if (message.Contains("Path:turn.end"))
                        break;
                }
            }

            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);

            return audioChunks.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket error generating audio");
            return null;
        }
    }

    private static string GenerateSecMsGec()
    {
        // Get current Unix timestamp
        var ticks = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Switch to Windows file time epoch (1601-01-01 00:00:00 UTC)
        ticks += WIN_EPOCH;

        // Round down to nearest 5 minutes (300 seconds)
        ticks -= ticks % 300;

        // Convert to 100-nanosecond intervals (Windows file time format)
        var ticksInNs = (double)ticks * S_TO_NS / 100;

        // Create string to hash
        var strToHash = $"{ticksInNs:F0}{API_KEY}";

        // Compute SHA256 hash and return uppercased hex digest
        var hashBytes = SHA256.HashData(Encoding.ASCII.GetBytes(strToHash));
        return Convert.ToHexString(hashBytes);
    }

    private string GenerateSsml(string text)
    {
        var escapedText = System.Security.SecurityElement.Escape(text);
        return $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='cs-CZ'>
            <voice name='{_voice}'>
                <prosody rate='{_rate}' volume='{_volume}' pitch='{_pitch}'>
                    {escapedText}
                </prosody>
            </voice>
        </speak>";
    }

    private async Task PlayAudioAsync(string audioFile, CancellationToken cancellationToken)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/ffplay",
                Arguments = $"-nodisp -autoexit -loglevel quiet \"{audioFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);
    }

    private string GetCacheFilePath(string text)
    {
        var safeName = new string(text
            .Take(50)
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray())
            .Trim('-')
            .ToLowerInvariant();

        var parameters = $"{_voice}{_rate}{_volume}{_pitch}";
        var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(parameters)))[..8];

        return Path.Combine(_cacheDirectory, $"{safeName}-{hash}.mp3");
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}
