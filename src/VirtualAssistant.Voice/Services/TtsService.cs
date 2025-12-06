using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Voice configuration for different AI clients.
/// </summary>
public sealed record VoiceConfig(string Voice, string Rate, string Volume, string Pitch);

/// <summary>
/// Text-to-Speech service using direct WebSocket to Microsoft Edge TTS API.
/// Pure C# - no external dependencies like Python edge-tts.
/// Supports multiple voice profiles for different AI clients.
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
    private readonly string _micLockFile = "/tmp/speech-lock";
    private readonly ConcurrentQueue<(string Text, string? Source)> _messageQueue = new();
    private readonly SemaphoreSlim _playbackLock = new(1, 1);
    private Process? _currentPlaybackProcess;

    /// <summary>
    /// Voice profiles for different AI clients loaded from configuration.
    /// Each client can have distinct voice, rate, volume, and pitch settings.
    /// </summary>
    private readonly Dictionary<string, VoiceConfig> _voiceProfiles;

    public TtsService(ILogger<TtsService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache", "virtual-assistant-tts");

        Directory.CreateDirectory(_cacheDirectory);

        // Load voice profiles from configuration with fallback to defaults
        var options = new TtsVoiceProfilesOptions();
        configuration.GetSection(TtsVoiceProfilesOptions.SectionName).Bind(options);
        _voiceProfiles = options.Profiles;

        _logger.LogInformation("Loaded {Count} TTS voice profiles: {Profiles}",
            _voiceProfiles.Count, string.Join(", ", _voiceProfiles.Keys));
    }

    /// <summary>
    /// Gets the voice configuration for a given source.
    /// Falls back to default if source is unknown.
    /// </summary>
    private VoiceConfig GetVoiceConfig(string? source)
    {
        if (string.IsNullOrEmpty(source) || !_voiceProfiles.TryGetValue(source, out var config))
        {
            return _voiceProfiles["default"];
        }
        return config;
    }

    /// <summary>
    /// Řekne text přes Microsoft Edge TTS API (přímé WebSocket volání).
    /// If speech lock is active, queues the message for later playback.
    /// </summary>
    /// <param name="text">Text to speak</param>
    /// <param name="source">AI client identifier for voice selection (e.g., "opencode", "claudecode")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task SpeakAsync(string text, string? source = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if microphone is active - queue message instead of discarding
            if (File.Exists(_micLockFile))
            {
                _messageQueue.Enqueue((text, source));
                _logger.LogInformation("🎤 Mikrofon aktivní - text zařazen do fronty ({QueueCount}): {Text}", 
                    _messageQueue.Count, text);
                return;
            }

            await SpeakDirectAsync(text, source, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TTS error for text: {Text}", text);
        }
    }

    /// <summary>
    /// Plays all queued messages. Called by DictationWorker after dictation ends.
    /// </summary>
    public async Task FlushQueueAsync(CancellationToken cancellationToken = default)
    {
        var count = _messageQueue.Count;
        if (count == 0)
        {
            _logger.LogDebug("FlushQueue: No messages in queue");
            return;
        }

        _logger.LogInformation("🔊 FlushQueue: Playing {Count} queued message(s)", count);

        while (_messageQueue.TryDequeue(out var item))
        {
            // Check if lock was re-acquired (user started dictating again)
            if (File.Exists(_micLockFile))
            {
                // Re-queue the message and stop flushing
                _messageQueue.Enqueue(item);
                _logger.LogInformation("🎤 Lock re-acquired during flush - stopping. Remaining: {Count}", 
                    _messageQueue.Count);
                return;
            }

            await SpeakDirectAsync(item.Text, item.Source, cancellationToken);
        }

        _logger.LogDebug("FlushQueue: All messages played");
    }

    /// <summary>
    /// Gets the number of messages currently in the queue.
    /// </summary>
    public int QueueCount => _messageQueue.Count;

    /// <summary>
    /// Stops any currently playing TTS audio immediately.
    /// Called when user presses CapsLock to start recording.
    /// </summary>
    public void StopPlayback()
    {
        try
        {
            var process = _currentPlaybackProcess;
            if (process != null && !process.HasExited)
            {
                _logger.LogInformation("🛑 Stopping TTS playback (CapsLock pressed)");
                process.Kill();
                _currentPlaybackProcess = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping TTS playback");
        }
    }

    /// <summary>
    /// Internal method to actually speak text (no queue check).
    /// Uses SemaphoreSlim to ensure only one message plays at a time.
    /// </summary>
    private async Task SpeakDirectAsync(string text, string? source, CancellationToken cancellationToken)
    {
        var voiceConfig = GetVoiceConfig(source);
        
        await _playbackLock.WaitAsync(cancellationToken);
        try
        {
            // Check cache first
            var cacheFile = GetCacheFilePath(text, voiceConfig);
            if (File.Exists(cacheFile))
            {
                _logger.LogDebug("Playing from cache: {Text} (source: {Source})", text, source ?? "default");
                await PlayAudioAsync(cacheFile, cancellationToken);
                return;
            }

            // Generate new audio via WebSocket
            _logger.LogDebug("Generating audio for: {Text} (source: {Source})", text, source ?? "default");
            var audioData = await GenerateAudioAsync(text, voiceConfig, cancellationToken);

            if (audioData == null || audioData.Length == 0)
            {
                _logger.LogWarning("Failed to generate audio for: {Text}", text);
                return;
            }

            // Save to cache
            await File.WriteAllBytesAsync(cacheFile, audioData, cancellationToken);

            // Play audio
            await PlayAudioAsync(cacheFile, cancellationToken);

            _logger.LogInformation("🗣️ TTS [{Source}]: \"{Text}\"", source ?? "default", text);
        }
        finally
        {
            _playbackLock.Release();
        }
    }

    private async Task<byte[]?> GenerateAudioAsync(string text, VoiceConfig voiceConfig, CancellationToken cancellationToken)
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

        _currentPlaybackProcess = process;
        try
        {
            process.Start();
            await process.WaitForExitAsync(cancellationToken);
        }
        finally
        {
            _currentPlaybackProcess = null;
        }
    }

    private string GetCacheFilePath(string text, VoiceConfig config)
    {
        var safeName = new string(text
            .Take(50)
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray())
            .Trim('-')
            .ToLowerInvariant();

        var parameters = $"{config.Voice}{config.Rate}{config.Volume}{config.Pitch}";
        var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(parameters)))[..8];

        return Path.Combine(_cacheDirectory, $"{safeName}-{hash}.mp3");
    }

    public void Dispose()
    {
        _playbackLock.Dispose();
    }
}
