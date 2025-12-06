using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Voice configuration for different AI clients.
/// </summary>
public sealed record VoiceConfig(string Voice, string Rate, string Volume, string Pitch);

/// <summary>
/// Text-to-Speech service with fallback support.
/// Primary: Microsoft Edge TTS (requires internet, may not work with VPN)
/// Fallback: Piper TTS (local, works offline)
/// </summary>
public sealed class TtsService : IDisposable
{
    private readonly ILogger<TtsService> _logger;
    private readonly string _cacheDirectory;
    private readonly string _micLockFile = "/tmp/speech-lock";
    private readonly ConcurrentQueue<(string Text, string? Source)> _messageQueue = new();
    private readonly SemaphoreSlim _playbackLock = new(1, 1);

    private readonly ITtsProvider _primaryProvider;
    private readonly ITtsProvider? _fallbackProvider;
    private readonly ILocationService? _locationService;
    private readonly TtsFallbackOptions _fallbackOptions;

    /// <summary>
    /// Voice profiles for different AI clients loaded from configuration.
    /// Each client can have distinct voice, rate, volume, and pitch settings.
    /// </summary>
    private readonly Dictionary<string, VoiceConfig> _voiceProfiles;

    // Track which provider is currently active
    private bool _useFallback;
    private DateTime _lastAvailabilityCheck = DateTime.MinValue;
    private const int AVAILABILITY_CHECK_INTERVAL_SECONDS = 60;

    public TtsService(
        ILogger<TtsService> logger,
        IConfiguration configuration,
        ITtsProvider primaryProvider,
        ITtsProvider? fallbackProvider = null,
        ILocationService? locationService = null,
        IOptions<TtsFallbackOptions>? fallbackOptions = null)
    {
        _logger = logger;
        _primaryProvider = primaryProvider;
        _fallbackProvider = fallbackProvider;
        _locationService = locationService;
        _fallbackOptions = fallbackOptions?.Value ?? new TtsFallbackOptions();

        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache", "virtual-assistant-tts");

        Directory.CreateDirectory(_cacheDirectory);

        // Load voice profiles from configuration with fallback to defaults
        var options = new TtsVoiceProfilesOptions();
        configuration.GetSection(TtsVoiceProfilesOptions.SectionName).Bind(options);
        _voiceProfiles = options.Profiles;

        _logger.LogInformation("TTS service initialized. Primary: {Primary}, Fallback: {Fallback}",
            _primaryProvider.Name,
            _fallbackProvider?.Name ?? "none");
        _logger.LogInformation("Loaded {Count} TTS voice profiles: {Profiles}",
            _voiceProfiles.Count, string.Join(", ", _voiceProfiles.Keys));
    }

    /// <summary>
    /// Gets the currently active provider name.
    /// </summary>
    public string ActiveProvider => _useFallback ? (_fallbackProvider?.Name ?? "none") : _primaryProvider.Name;

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
    /// Speaks text using the best available TTS provider.
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
    /// Internal method to actually speak text (no queue check).
    /// Uses SemaphoreSlim to ensure only one message plays at a time.
    /// Implements fallback logic when primary provider fails.
    /// </summary>
    private async Task SpeakDirectAsync(string text, string? source, CancellationToken cancellationToken)
    {
        var voiceConfig = GetVoiceConfig(source);

        await _playbackLock.WaitAsync(cancellationToken);
        try
        {
            // Check cache first (provider-agnostic)
            var cacheFile = GetCacheFilePath(text, voiceConfig);
            if (File.Exists(cacheFile))
            {
                _logger.LogDebug("Playing from cache: {Text} (source: {Source})", text, source ?? "default");
                await PlayAudioAsync(cacheFile, cancellationToken);
                return;
            }

            // Determine which provider to use
            await UpdateProviderSelectionAsync(cancellationToken);

            // Generate audio with fallback
            var audioData = await GenerateAudioWithFallbackAsync(text, voiceConfig, cancellationToken);

            if (audioData == null || audioData.Length == 0)
            {
                if (_fallbackOptions.SilentOnFailure)
                {
                    _logger.LogWarning("All TTS providers failed for: {Text}", text);
                    return;
                }
                throw new InvalidOperationException($"Failed to generate audio for: {text}");
            }

            // Save to cache
            await File.WriteAllBytesAsync(cacheFile, audioData, cancellationToken);

            // Play audio
            await PlayAudioAsync(cacheFile, cancellationToken);

            _logger.LogInformation("🗣️ TTS [{Provider}][{Source}]: \"{Text}\"",
                ActiveProvider, source ?? "default", text);
        }
        finally
        {
            _playbackLock.Release();
        }
    }

    /// <summary>
    /// Updates provider selection based on location/VPN status.
    /// </summary>
    private async Task UpdateProviderSelectionAsync(CancellationToken cancellationToken)
    {
        if (!_fallbackOptions.EnableFallback || _fallbackProvider == null)
            return;

        // Check VPN status if location service is available
        if (_fallbackOptions.CheckLocation && _locationService != null)
        {
            try
            {
                var isVpnActive = await _locationService.IsVpnActiveAsync(cancellationToken);
                if (isVpnActive && !_useFallback)
                {
                    _logger.LogInformation("VPN detected - switching to fallback provider ({Provider})",
                        _fallbackProvider.Name);
                    _useFallback = true;
                    return;
                }
                else if (!isVpnActive && _useFallback)
                {
                    _logger.LogInformation("VPN not detected - switching back to primary provider ({Provider})",
                        _primaryProvider.Name);
                    _useFallback = false;
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Location check failed, continuing with current provider");
            }
        }

        // Periodic availability check
        if (_fallbackOptions.AlwaysCheckAvailability &&
            DateTime.UtcNow - _lastAvailabilityCheck > TimeSpan.FromSeconds(AVAILABILITY_CHECK_INTERVAL_SECONDS))
        {
            _lastAvailabilityCheck = DateTime.UtcNow;

            if (!_useFallback)
            {
                var isAvailable = await _primaryProvider.IsAvailableAsync(cancellationToken);
                if (!isAvailable)
                {
                    _logger.LogWarning("Primary provider unavailable - switching to fallback");
                    _useFallback = true;
                }
            }
            else
            {
                // Check if we can switch back to primary
                var isAvailable = await _primaryProvider.IsAvailableAsync(cancellationToken);
                if (isAvailable)
                {
                    _logger.LogInformation("Primary provider available again - switching back");
                    _useFallback = false;
                }
            }
        }
    }

    /// <summary>
    /// Generates audio with automatic fallback on failure.
    /// </summary>
    private async Task<byte[]?> GenerateAudioWithFallbackAsync(
        string text,
        VoiceConfig voiceConfig,
        CancellationToken cancellationToken)
    {
        var provider = _useFallback ? _fallbackProvider : _primaryProvider;

        if (provider == null)
        {
            _logger.LogError("No TTS provider available");
            return null;
        }

        _logger.LogDebug("Generating audio using {Provider} for: {Text}", provider.Name, text);
        var audioData = await provider.GenerateAudioAsync(text, voiceConfig, cancellationToken);

        // If primary failed and fallback is available, try fallback
        if ((audioData == null || audioData.Length == 0) &&
            !_useFallback &&
            _fallbackOptions.EnableFallback &&
            _fallbackProvider != null)
        {
            _logger.LogWarning("Primary provider failed, trying fallback ({Provider})", _fallbackProvider.Name);
            _useFallback = true;

            audioData = await _fallbackProvider.GenerateAudioAsync(text, voiceConfig, cancellationToken);
        }

        return audioData;
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

    private string GetCacheFilePath(string text, VoiceConfig config)
    {
        var safeName = new string(text
            .Take(50)
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray())
            .Trim('-')
            .ToLowerInvariant();

        // Include provider in cache key to avoid mixing Edge and Piper audio
        var providerKey = _useFallback ? "piper" : "edge";
        var parameters = $"{providerKey}{config.Voice}{config.Rate}{config.Volume}{config.Pitch}";
        var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(parameters)))[..8];

        return Path.Combine(_cacheDirectory, $"{safeName}-{hash}.mp3");
    }

    public void Dispose()
    {
        _playbackLock.Dispose();
    }
}
