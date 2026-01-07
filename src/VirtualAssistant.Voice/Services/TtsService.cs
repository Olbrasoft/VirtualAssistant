using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Voice configuration for different AI clients.
/// </summary>
public sealed record VoiceConfig(string Voice, string Rate, string Volume, string Pitch, string? Provider = null);

/// <summary>
/// Text-to-Speech service orchestrator.
/// Coordinates queue, cache, playback, and speech lock services.
/// Uses provider chain with circuit breaker for resilience.
/// </summary>
public sealed class TtsService : IDisposable
{
    private readonly ILogger<TtsService> _logger;
    private readonly ITtsProviderChain _ttsProviderChain;
    private readonly ITtsQueueService _queueService;
    private readonly ITtsCacheService _cacheService;
    private readonly IAudioPlaybackService _playbackService;
    private readonly ISpeechLockService _speechLockService;
    private readonly SemaphoreSlim _playbackLock = new(1, 1);
    private readonly Dictionary<string, TtsProfile> _ttsProfiles;
    private readonly TtsProfile _defaultProfile;

    public TtsService(
        ILogger<TtsService> logger,
        IOptions<TtsProfilesOptions> ttsProfilesOptions,
        ITtsProviderChain ttsProviderChain,
        ITtsQueueService queueService,
        ITtsCacheService cacheService,
        IAudioPlaybackService playbackService,
        ISpeechLockService speechLockService)
    {
        _logger = logger;
        _ttsProviderChain = ttsProviderChain;
        _queueService = queueService;
        _cacheService = cacheService;
        _playbackService = playbackService;
        _speechLockService = speechLockService;

        // Load TTS profiles from options
        var options = ttsProfilesOptions.Value;
        _ttsProfiles = new Dictionary<string, TtsProfile>(options.Profiles, StringComparer.OrdinalIgnoreCase);
        _defaultProfile = options.DefaultProfile;

        _logger.LogInformation("TTS Service initialized with {ProfileCount} profiles (default: {DefaultVoice})",
            _ttsProfiles.Count, _defaultProfile.Voice);

        // Log loaded profiles
        foreach (var (name, profile) in _ttsProfiles)
        {
            _logger.LogDebug("TTS profile '{ProfileName}': {Provider} - {Voice}", name, profile.Provider, profile.Voice);
        }
    }

    /// <summary>
    /// Gets the voice configuration for the specified source (agent).
    /// Maps source to TTS profile from appsettings.json.
    /// </summary>
    private VoiceConfig GetVoiceConfig(string? source)
    {
        TtsProfile profile;

        if (!string.IsNullOrEmpty(source) && _ttsProfiles.TryGetValue(source, out var foundProfile))
        {
            profile = foundProfile;
            _logger.LogDebug("Using TTS profile '{Source}': {Provider} - {Voice}", source, profile.Provider, profile.Voice);
        }
        else
        {
            profile = _defaultProfile;
            _logger.LogDebug("Using default TTS profile: {Provider} - {Voice}", profile.Provider, profile.Voice);
        }

        return new VoiceConfig(profile.Voice, profile.Rate, "100", profile.Pitch, profile.Provider);
    }

    /// <summary>
    /// Speaks text via TTS provider.
    /// If speech lock is active, queues the message for later playback.
    /// </summary>
    /// <param name="text">Text to speak</param>
    /// <param name="source">AI client identifier for voice selection (e.g., "opencode", "claudecode")</param>
    /// <param name="skipCache">If true, bypasses cache and always generates fresh audio</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple indicating success and the provider used (if successful)</returns>
    public async Task<(bool Success, string? ProviderUsed)> SpeakAsync(string text, string? source = null, bool skipCache = false, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if microphone is active - queue message instead of discarding
            if (_speechLockService.IsLocked)
            {
                _queueService.Enqueue(text, source);
                _logger.LogInformation("🎤 Mikrofon aktivní - text zařazen do fronty ({QueueCount}): {Text}",
                    _queueService.Count, text);
                return (false, null); // Queued, not played yet
            }

            return await SpeakDirectAsync(text, source, skipCache, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TTS error for text: {Text}", text);
            return (false, null);
        }
    }

    /// <summary>
    /// Plays all queued messages. Called by DictationWorker after dictation ends.
    /// </summary>
    public async Task FlushQueueAsync(CancellationToken cancellationToken = default)
    {
        var count = _queueService.Count;
        if (count == 0)
        {
            _logger.LogDebug("FlushQueue: No messages in queue");
            return;
        }

        _logger.LogInformation("🔊 FlushQueue: Playing {Count} queued message(s)", count);

        while (_queueService.TryDequeue(out var item))
        {
            // Check if lock was re-acquired (user started dictating again)
            if (_speechLockService.IsLocked)
            {
                // Re-queue the message and stop flushing
                _queueService.Enqueue(item.Text, item.Source);
                _logger.LogInformation("🎤 Lock re-acquired during flush - stopping. Remaining: {Count}",
                    _queueService.Count);
                return;
            }

            await SpeakDirectAsync(item.Text, item.Source, skipCache: false, cancellationToken);
        }

        _logger.LogDebug("FlushQueue: All messages played");
    }

    /// <summary>
    /// Gets the number of messages currently in the queue.
    /// </summary>
    public int QueueCount => _queueService.Count;

    /// <summary>
    /// Gets whether audio is currently playing (excludes generation phase).
    /// </summary>
    public bool IsPlaying => _playbackService.IsPlaying;

    /// <summary>
    /// Stops any currently playing TTS audio immediately.
    /// Called when user presses CapsLock to start recording.
    /// </summary>
    public void StopPlayback()
    {
        _playbackService.Stop();
    }

    /// <summary>
    /// Internal method to actually speak text (no queue check).
    /// Uses SemaphoreSlim to ensure only one message plays at a time.
    /// </summary>
    /// <returns>Tuple indicating success and the provider used (if successful)</returns>
    private async Task<(bool Success, string? ProviderUsed)> SpeakDirectAsync(string text, string? source, bool skipCache, CancellationToken cancellationToken)
    {
        var voiceConfig = GetVoiceConfig(source);

        await _playbackLock.WaitAsync(cancellationToken);
        try
        {
            // CRITICAL: Check lock AGAIN before any playback - user may have started recording
            if (_speechLockService.IsLocked)
            {
                _queueService.Enqueue(text, source);
                _logger.LogInformation("🎤 Lock detected in SpeakDirect - queuing: {Text}", text);
                return (false, null);
            }

            // Check cache first (unless skipCache is true)
            if (!skipCache && _cacheService.TryGetCached(text, voiceConfig, out var cacheFile))
            {
                _logger.LogDebug("Playing from cache: {Text} (source: {Source})", text, source ?? "default");

                // Check lock before playback
                if (_speechLockService.IsLocked)
                {
                    _queueService.Enqueue(text, source);
                    _logger.LogInformation("🎤 Lock detected before cache playback - queuing: {Text}", text);
                    return (false, null);
                }

                await _playbackService.PlayAsync(cacheFile, cancellationToken);
                return (true, "cache"); // Successful playback from cache
            }

            if (skipCache)
            {
                _logger.LogDebug("Skipping cache (skipCache=true): {Text}", text);
            }

            // Generate new audio via provider chain (with circuit breaker)
            _logger.LogDebug("Generating audio for: {Text} (source: {Source})", text, source ?? "default");
            var (audioData, providerUsed) = await _ttsProviderChain.SynthesizeAsync(text, voiceConfig, source, cancellationToken);

            if (audioData == null || audioData.Length == 0)
            {
                _logger.LogWarning("All TTS providers failed for: {Text}", text);
                return (false, null);
            }

            _logger.LogDebug("Audio generated by {Provider} for: {Text}", providerUsed, text);

            // Save to cache
            await _cacheService.SaveAsync(text, voiceConfig, audioData, cancellationToken);
            var audioFile = _cacheService.GetCachePath(text, voiceConfig);

            // CRITICAL: Check lock AGAIN before playing - generation may have taken time
            if (_speechLockService.IsLocked)
            {
                _queueService.Enqueue(text, source);
                _logger.LogInformation("🎤 Lock detected after generation - queuing: {Text}", text);
                return (false, null);
            }

            // Play audio
            await _playbackService.PlayAsync(audioFile, cancellationToken);

            _logger.LogInformation("🗣️ TTS [{Source}] via {Provider}: \"{Text}\"", source ?? "default", providerUsed, text);

            return (true, providerUsed);
        }
        finally
        {
            _playbackLock.Release();
        }
    }

    /// <summary>
    /// Releases resources used by the TTS service, including the playback lock semaphore.
    /// </summary>
    public void Dispose()
    {
        _playbackLock.Dispose();
    }
}
