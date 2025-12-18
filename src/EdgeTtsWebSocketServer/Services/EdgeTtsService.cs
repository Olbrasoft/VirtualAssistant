using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using System.Net.Http.Json;

namespace Olbrasoft.VoiceAssistant.EdgeTtsWebSocketServer.Services;

public class EdgeTtsService
{
    // Updated to match Python edge-tts 7.2.7
    private const string BASE_URL = "speech.platform.bing.com/consumer/speech/synthesize/readaloud";
    private const string TRUSTED_CLIENT_TOKEN = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    private const string WSS_URL = $"wss://{BASE_URL}/edge/v1?TrustedClientToken={TRUSTED_CLIENT_TOKEN}";
    private const string CHROMIUM_FULL_VERSION = "143.0.3650.75";
    private const long WIN_EPOCH = 11644473600;
    private const double S_TO_NS = 1e9;
    
    private readonly string _cacheDirectory;
    private readonly string _speechLockFile;
    private readonly string _defaultVoice;
    private readonly string _defaultRate;
    private readonly string _outputFormat;
    private readonly string _listenerApiUrl;
    private readonly ILogger<EdgeTtsService> _logger;
    private readonly HttpClient _httpClient;
    
    // Current playback process for stop functionality
    private Process? _currentPlaybackProcess;
    private readonly object _processLock = new();

    public EdgeTtsService(
        IConfiguration configuration,
        ILogger<EdgeTtsService> logger,
        HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        _cacheDirectory = ExpandPath(configuration["EdgeTts:CacheDirectory"] ?? "~/.cache/edge-tts-server");
        _speechLockFile = configuration["EdgeTts:SpeechLockFile"] ?? "/tmp/speech.lock";
        _defaultVoice = configuration["EdgeTts:DefaultVoice"] ?? "cs-CZ-AntoninNeural";
        _defaultRate = configuration["EdgeTts:DefaultRate"] ?? "+20%";
        _outputFormat = configuration["EdgeTts:OutputFormat"] ?? "audio-24khz-48kbitrate-mono-mp3";
        _listenerApiUrl = configuration["EdgeTts:ListenerApiUrl"] ?? "http://localhost:5051";

        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<(bool success, string message, bool cached)> SpeakAsync(
        string text, 
        string? voice = null, 
        string? rate = null,
        string? volume = null,
        string? pitch = null,
        bool play = true)
    {
        try
        {
            voice ??= _defaultVoice;
            rate ??= _defaultRate;
            volume ??= "+0%";
            pitch ??= "+0Hz";

            // Generate cache file name
            var cacheFileName = GenerateCacheFileName(text, voice, rate, volume, pitch);
            var cacheFilePath = Path.Combine(_cacheDirectory, cacheFileName);

            // Check cache
            if (File.Exists(cacheFilePath))
            {
                _logger.LogInformation("Playing from cache: {Text}", text);
                if (play)
                {
                    await PlayAudioAsync(cacheFilePath, text);
                    return (true, $"✅ Played from cache: {text}", true);
                }
                return (true, $"✅ Audio cached at: {cacheFilePath}", true);
            }

            // Generate new audio via WebSocket
            var audioData = await GenerateAudioAsync(text, voice, rate, volume, pitch);
            
            if (audioData == null || audioData.Length == 0)
            {
                return (false, "❌ Failed to generate audio", false);
            }

            // Save to cache
            await File.WriteAllBytesAsync(cacheFilePath, audioData);
            _logger.LogInformation("Saved to cache: {CacheFile}", cacheFilePath);

            // Play audio
            if (play)
            {
                await PlayAudioAsync(cacheFilePath, text);
                return (true, $"✅ Generated and played: {text}", false);
            }

            return (true, $"✅ Audio generated at: {cacheFilePath}", false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SpeakAsync");
            return (false, $"❌ Error: {ex.Message}", false);
        }
    }

    private async Task<byte[]?> GenerateAudioAsync(string text, string voice, string rate, string volume, string pitch)
    {
        using var client = new ClientWebSocket();
        ConfigureWebSocketHeaders(client);

        var uri = BuildWebSocketUri();

        try
        {
            await client.ConnectAsync(uri, CancellationToken.None);
            _logger.LogInformation("Connected to Microsoft Edge TTS WebSocket at {Uri}", uri);

            await SendSpeechConfigAsync(client);
            await SendSsmlRequestAsync(client, text, voice, rate, volume, pitch);
            var audioData = await ReceiveAudioDataAsync(client);

            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);

            _logger.LogInformation("Total audio data collected: {Count} bytes", audioData.Length);

            return audioData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating audio via WebSocket");
            return null;
        }
    }

    private void ConfigureWebSocketHeaders(ClientWebSocket client)
    {
        var chromiumMajor = CHROMIUM_FULL_VERSION.Split('.')[0];
        client.Options.SetRequestHeader("User-Agent",
            $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{chromiumMajor}.0.0.0 Safari/537.36 Edg/{chromiumMajor}.0.0.0");
        client.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
        client.Options.SetRequestHeader("Pragma", "no-cache");
        client.Options.SetRequestHeader("Cache-Control", "no-cache");
        client.Options.SetRequestHeader("Accept-Encoding", "gzip, deflate, br, zstd");
        client.Options.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");

        var muid = GenerateMuid();
        client.Options.SetRequestHeader("Cookie", $"muid={muid};");
    }

    private static Uri BuildWebSocketUri()
    {
        var connectionId = Guid.NewGuid().ToString("N");
        var secMsGec = GenerateSecMsGec();
        var secMsGecVersion = $"1-{CHROMIUM_FULL_VERSION}";
        return new Uri($"{WSS_URL}&ConnectionId={connectionId}&Sec-MS-GEC={secMsGec}&Sec-MS-GEC-Version={secMsGecVersion}");
    }

    private async Task SendSpeechConfigAsync(ClientWebSocket client)
    {
        var timestamp = DateToString();
        var jsonPayload = "{\"context\":{\"synthesis\":{\"audio\":{\"metadataoptions\":{" +
                         "\"sentenceBoundaryEnabled\":\"true\",\"wordBoundaryEnabled\":\"false\"}," +
                         "\"outputFormat\":\"" + _outputFormat + "\"}}}}";

        var configMessage = $"X-Timestamp:{timestamp}\r\n" +
                           "Content-Type:application/json; charset=utf-8\r\n" +
                           "Path:speech.config\r\n\r\n" +
                           jsonPayload + "\r\n";

        await client.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes(configMessage)),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);
    }

    private async Task SendSsmlRequestAsync(ClientWebSocket client, string text, string voice, string rate, string volume, string pitch)
    {
        var timestamp = DateToString();
        var requestId = Guid.NewGuid().ToString("N");
        var ssml = GenerateSsml(text, voice, rate, volume, pitch);
        var ssmlMessage = $"X-RequestId:{requestId}\r\n" +
                         "Content-Type:application/ssml+xml\r\n" +
                         $"X-Timestamp:{timestamp}Z\r\n" +
                         "Path:ssml\r\n\r\n" +
                         ssml;

        await client.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes(ssmlMessage)),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);
    }

    private async Task<byte[]> ReceiveAudioDataAsync(ClientWebSocket client)
    {
        var audioChunks = new List<byte>();
        var buffer = new byte[16384];

        while (client.State == WebSocketState.Open)
        {
            var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                _logger.LogInformation("WebSocket closed by server");
                break;
            }

            if (result.MessageType == WebSocketMessageType.Binary)
            {
                ProcessBinaryMessage(buffer, result.Count, audioChunks);
            }
            else if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                _logger.LogDebug("Received text message: {Message}", message.Length > 100 ? message[..100] + "..." : message);

                if (message.Contains("Path:turn.end"))
                {
                    _logger.LogInformation("Received turn.end - audio generation complete");
                    break;
                }
            }
        }

        return audioChunks.ToArray();
    }

    private void ProcessBinaryMessage(byte[] buffer, int count, List<byte> audioChunks)
    {
        _logger.LogDebug("Received binary message: {Count} bytes", count);

        if (count < 2)
        {
            _logger.LogWarning("Binary message too short (< 2 bytes)");
            return;
        }

        var headerLength = (buffer[0] << 8) | buffer[1];
        var audioStart = 2 + headerLength;

        if (audioStart > count)
        {
            _logger.LogWarning("Header length {HeaderLength} exceeds message size {Count}", headerLength, count);
            return;
        }

        var headerBytes = buffer.Skip(2).Take(headerLength).ToArray();
        var headerText = Encoding.UTF8.GetString(headerBytes);

        if (headerText.Contains("Path:audio"))
        {
            var audioBytes = count - audioStart;
            audioChunks.AddRange(buffer.Skip(audioStart).Take(audioBytes));
            _logger.LogDebug("Added {AudioBytes} bytes of audio data. Total: {Total} bytes", audioBytes, audioChunks.Count);
        }
    }

    private static string GenerateSsml(string text, string voice, string rate, string volume, string pitch)
    {
        // Matching Python edge-tts format exactly
        var escapedText = System.Security.SecurityElement.Escape(text);
        return "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>" +
               $"<voice name='{voice}'>" +
               $"<prosody pitch='{pitch}' rate='{rate}' volume='{volume}'>" +
               escapedText +
               "</prosody>" +
               "</voice>" +
               "</speak>";
    }

    /// <summary>
    /// Returns JavaScript-style date string matching Python edge-tts format.
    /// </summary>
    private static string DateToString()
    {
        return DateTime.UtcNow.ToString("ddd MMM dd yyyy HH:mm:ss 'GMT+0000 (Coordinated Universal Time)'",
            System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Generates a random MUID (matching Python edge-tts).
    /// </summary>
    private static string GenerateMuid()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes);
    }

    private string GenerateCacheFileName(string text, string voice, string rate, string volume, string pitch)
    {
        var safeName = new string(text
            .Take(50)
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray())
            .Trim('-')
            .ToLowerInvariant();

        var parameters = $"{voice}{rate}{volume}{pitch}";
        var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(parameters)))[..8];

        return $"{safeName}-{hash}.mp3";
    }

    private async Task PlayAudioAsync(string audioFile, string spokenText)
    {
        // Acquire speech lock
        using var lockFile = new FileStream(_speechLockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        Process? process = null;
        try
        {
            // Notify ContinuousListener what we're about to say
            await NotifyListenerSpeechStartAsync(spokenText);

            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffplay",
                    Arguments = $"-nodisp -autoexit -loglevel quiet \"{audioFile}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }
            };

            // Store reference for stop functionality
            lock (_processLock)
            {
                _currentPlaybackProcess = process;
            }

            process.Start();
            await process.WaitForExitAsync();
        }
        finally
        {
            // Notify ContinuousListener that we stopped speaking
            await NotifyListenerSpeechEndAsync();

            lock (_processLock)
            {
                _currentPlaybackProcess = null;
            }

            process?.Dispose();
            lockFile.Close();
            File.Delete(_speechLockFile);
        }
    }
    
    /// <summary>
    /// Notifies ContinuousListener that assistant is starting to speak.
    /// </summary>
    private async Task NotifyListenerSpeechStartAsync(string text)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_listenerApiUrl}/api/assistant-speech/start", 
                new { text });
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to notify listener of speech start: {Status}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Don't fail TTS if listener notification fails
            _logger.LogDebug(ex, "Could not notify listener of speech start (listener may not be running)");
        }
    }
    
    /// <summary>
    /// Notifies ContinuousListener that assistant stopped speaking.
    /// </summary>
    private async Task NotifyListenerSpeechEndAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"{_listenerApiUrl}/api/assistant-speech/end", null);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to notify listener of speech end: {Status}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Don't fail TTS if listener notification fails
            _logger.LogDebug(ex, "Could not notify listener of speech end (listener may not be running)");
        }
    }

    /// <summary>
    /// Stops current speech playback immediately.
    /// </summary>
    /// <returns>True if playback was stopped, false if nothing was playing.</returns>
    public bool StopSpeaking()
    {
        lock (_processLock)
        {
            if (_currentPlaybackProcess == null || _currentPlaybackProcess.HasExited)
            {
                _logger.LogInformation("StopSpeaking: No active playback to stop");
                return false;
            }

            try
            {
                _logger.LogInformation("StopSpeaking: Killing ffplay process {Pid}", _currentPlaybackProcess.Id);
                _currentPlaybackProcess.Kill(entireProcessTree: true);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "StopSpeaking: Failed to kill process");
                return false;
            }
        }
    }

    private static string ExpandPath(string path)
    {
        if (path.StartsWith("~/"))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
        }
        return path;
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
        var strToHash = $"{ticksInNs:F0}{TRUSTED_CLIENT_TOKEN}";
        
        // Compute SHA256 hash and return uppercased hex digest
        var hashBytes = SHA256.HashData(Encoding.ASCII.GetBytes(strToHash));
        return Convert.ToHexString(hashBytes);
    }

    public int ClearCache()
    {
        try
        {
            var files = Directory.GetFiles(_cacheDirectory, "*.mp3");
            foreach (var file in files)
            {
                File.Delete(file);
            }
            return files.Length;
        }
        catch
        {
            return 0;
        }
    }
}
