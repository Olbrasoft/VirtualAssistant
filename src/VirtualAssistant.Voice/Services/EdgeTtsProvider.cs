using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// TTS provider using Microsoft Edge TTS WebSocket API.
/// Requires internet connection to Microsoft services.
/// </summary>
public sealed class EdgeTtsProvider : ITtsProvider
{
    private const string WSS_URL = "wss://api.msedgeservices.com/tts/cognitiveservices/websocket/v1";
    private const string API_KEY = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    private const string CHROMIUM_FULL_VERSION = "140.0.3485.14";
    private const long WIN_EPOCH = 11644473600;
    private const double S_TO_NS = 1e9;
    private const int CONNECTION_TIMEOUT_MS = 5000;

    private readonly ILogger<EdgeTtsProvider> _logger;

    public string Name => "EdgeTTS";

    public EdgeTtsProvider(ILogger<EdgeTtsProvider> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new ClientWebSocket();
            ConfigureWebSocket(client);

            var connectionId = Guid.NewGuid().ToString("N");
            var uri = BuildUri(connectionId);

            using var timeoutCts = new CancellationTokenSource(CONNECTION_TIMEOUT_MS);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await client.ConnectAsync(uri, linkedCts.Token);
            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Availability check", CancellationToken.None);

            _logger.LogDebug("EdgeTTS availability check: OK");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "EdgeTTS availability check failed");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<byte[]?> GenerateAudioAsync(string text, VoiceConfig voiceConfig, CancellationToken cancellationToken = default)
    {
        using var client = new ClientWebSocket();
        ConfigureWebSocket(client);

        var connectionId = Guid.NewGuid().ToString("N");
        var uri = BuildUri(connectionId);

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
            var ssml = GenerateSsml(text, voiceConfig);
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
            _logger.LogError(ex, "EdgeTTS WebSocket error generating audio");
            return null;
        }
    }

    private static void ConfigureWebSocket(ClientWebSocket client)
    {
        client.Options.SetRequestHeader("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36 Edg/140.0.0.0");
        client.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
        client.Options.SetRequestHeader("Pragma", "no-cache");
        client.Options.SetRequestHeader("Cache-Control", "no-cache");
        client.Options.SetRequestHeader("Accept-Encoding", "gzip, deflate, br");
        client.Options.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
        client.Options.AddSubProtocol("synthesize");
    }

    private static Uri BuildUri(string connectionId)
    {
        var secMsGec = GenerateSecMsGec();
        var secMsGecVersion = $"1-{CHROMIUM_FULL_VERSION}";
        return new Uri($"{WSS_URL}?Ocp-Apim-Subscription-Key={API_KEY}&ConnectionId={connectionId}&Sec-MS-GEC={secMsGec}&Sec-MS-GEC-Version={secMsGecVersion}");
    }

    private static string GenerateSecMsGec()
    {
        var ticks = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        ticks += WIN_EPOCH;
        ticks -= ticks % 300;
        var ticksInNs = (double)ticks * S_TO_NS / 100;
        var strToHash = $"{ticksInNs:F0}{API_KEY}";
        var hashBytes = SHA256.HashData(Encoding.ASCII.GetBytes(strToHash));
        return Convert.ToHexString(hashBytes);
    }

    private static string GenerateSsml(string text, VoiceConfig config)
    {
        var escapedText = System.Security.SecurityElement.Escape(text);
        return $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='cs-CZ'>
            <voice name='{config.Voice}'>
                <prosody rate='{config.Rate}' volume='{config.Volume}' pitch='{config.Pitch}'>
                    {escapedText}
                </prosody>
            </voice>
        </speak>";
    }
}
