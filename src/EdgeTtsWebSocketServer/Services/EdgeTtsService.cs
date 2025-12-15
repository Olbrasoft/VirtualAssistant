using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using System.Net.Http.Json;

namespace Olbrasoft.VoiceAssistant.EdgeTtsWebSocketServer.Services;

public class EdgeTtsService
{
    private const string WSS_URL = "wss://api.msedgeservices.com/tts/cognitiveservices/websocket/v1";
    private const string API_KEY = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    private const string CHROMIUM_FULL_VERSION = "131.0.2903.86";
    private const long WIN_EPOCH = 11644473600;
    private const double S_TO_NS = 1e9;
    
    private readonly string _cacheDirectory;
    private readonly string _speechLockFile;
    private readonly string _defaultVoice;
    private readonly string _defaultRate;
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
        
        // Add all required headers to match Microsoft Edge TTS requirements
        client.Options.SetRequestHeader("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36 Edg/140.0.0.0");
        client.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
        client.Options.SetRequestHeader("Pragma", "no-cache");
        client.Options.SetRequestHeader("Cache-Control", "no-cache");
        client.Options.SetRequestHeader("Accept-Encoding", "gzip, deflate, br");
        client.Options.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
        
        // CRITICAL: Add WebSocket subprotocol - this is required by Microsoft Edge TTS
        client.Options.AddSubProtocol("synthesize");
        
        // Generate connection parameters
        var connectionId = Guid.NewGuid().ToString("N");
        var secMsGec = GenerateSecMsGec();
        var secMsGecVersion = $"1-{CHROMIUM_FULL_VERSION}";
        
        var uri = new Uri($"{WSS_URL}?Ocp-Apim-Subscription-Key={API_KEY}&ConnectionId={connectionId}&Sec-MS-GEC={secMsGec}&Sec-MS-GEC-Version={secMsGecVersion}");
        
        try
        {
            await client.ConnectAsync(uri, CancellationToken.None);
            _logger.LogInformation("Connected to Microsoft Edge TTS WebSocket");

            // Send SSML request
            var requestId = Guid.NewGuid().ToString("N");
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffK");
            
            var ssml = GenerateSsml(text, voice, rate, volume, pitch);
            var configMessage = $"X-Timestamp:{timestamp}\r\nContent-Type:application/json; charset=utf-8\r\nPath:speech.config\r\n\r\n" +
                               $"{{\"context\":{{\"synthesis\":{{\"audio\":{{\"metadataoptions\":{{\"sentenceBoundaryEnabled\":\"false\",\"wordBoundaryEnabled\":\"false\"}},\"outputFormat\":\"audio-24khz-48kbitrate-mono-mp3\"}}}}}}}}";
            
            await client.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(configMessage)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);

            var ssmlMessage = $"X-RequestId:{requestId}\r\nContent-Type:application/ssml+xml\r\nPath:ssml\r\n\r\n{ssml}";
            
            await client.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(ssmlMessage)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);

            // Receive audio data
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
                    _logger.LogInformation($"Received binary message: {result.Count} bytes");
                    
                    // Message must be at least 2 bytes to contain header length
                    if (result.Count < 2)
                    {
                        _logger.LogWarning("Binary message too short (< 2 bytes)");
                        continue;
                    }
                    
                    // First 2 bytes contain header length in BIG-ENDIAN format
                    var headerLength = (buffer[0] << 8) | buffer[1];
                    _logger.LogInformation($"Header length (big-endian): {headerLength}");
                    
                    // Header starts at byte 2 and continues for headerLength bytes
                    // Audio data starts after that
                    var audioStart = 2 + headerLength;
                    
                    if (audioStart > result.Count)
                    {
                        _logger.LogWarning($"Header length {headerLength} exceeds message size {result.Count}");
                        continue;
                    }
                    
                    // Parse headers to check if this is audio data
                    var headerBytes = buffer.Skip(2).Take(headerLength).ToArray();
                    var headerText = Encoding.UTF8.GetString(headerBytes);
                    
                    if (headerText.Contains("Path:audio"))
                    {
                        var audioBytes = result.Count - audioStart;
                        audioChunks.AddRange(buffer.Skip(audioStart).Take(audioBytes));
                        _logger.LogInformation($"Added {audioBytes} bytes of audio data. Total so far: {audioChunks.Count} bytes");
                    }
                    else
                    {
                        _logger.LogInformation($"Binary message is not audio data: {headerText.Substring(0, Math.Min(50, headerText.Length))}");
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    _logger.LogInformation($"Received text message: {message.Substring(0, Math.Min(100, message.Length))}...");
                    
                    // Check if this is the end message
                    if (message.Contains("Path:turn.end"))
                    {
                        _logger.LogInformation("Received turn.end - audio generation complete");
                        break;
                    }
                }
            }

            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
            
            _logger.LogInformation($"Total audio data collected: {audioChunks.Count} bytes");
            
            return audioChunks.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating audio via WebSocket");
            return null;
        }
    }

    private string GenerateSsml(string text, string voice, string rate, string volume, string pitch)
    {
        return $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='cs-CZ'>
            <voice name='{voice}'>
                <prosody rate='{rate}' volume='{volume}' pitch='{pitch}'>
                    {System.Security.SecurityElement.Escape(text)}
                </prosody>
            </voice>
        </speak>";
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
        var strToHash = $"{ticksInNs:F0}{API_KEY}";
        
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
