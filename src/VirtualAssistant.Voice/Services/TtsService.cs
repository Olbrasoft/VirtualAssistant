using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Voice configuration for different AI clients.
/// </summary>
public sealed record VoiceConfig(string Voice, string Rate, string Volume, string Pitch);

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
    private readonly VoiceConfig _voiceConfig;

    public TtsService(
        ILogger<TtsService> logger,
        IConfiguration configuration,
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

        // Voice config is now provider-specific in TTS:EdgeTTS, TTS:AzureTTS, etc.
        // Use empty config - each provider reads its own configuration
        _voiceConfig = new VoiceConfig("", "", "", "");

        _logger.LogInformation("TTS Service initialized - providers use their own voice configuration");
    }

    /// <summary>
    /// Gets the voice configuration (single config for all sources).
    /// </summary>
    private VoiceConfig GetVoiceConfig(string? source) => _voiceConfig;

    /// <summary>
    /// Speaks text via TTS provider.
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
            if (_speechLockService.IsLocked)
            {
                _queueService.Enqueue(text, source);
                _logger.LogInformation("🎤 Mikrofon aktivní - text zařazen do fronty ({QueueCount}): {Text}",
                    _queueService.Count, text);
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

            await SpeakDirectAsync(item.Text, item.Source, cancellationToken);
        }

        _logger.LogDebug("FlushQueue: All messages played");
    }

    /// <summary>
    /// Gets the number of messages currently in the queue.
    /// </summary>
    public int QueueCount => _queueService.Count;

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
    private async Task SpeakDirectAsync(string text, string? source, CancellationToken cancellationToken)
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
                return;
            }

            // Check cache first
            if (_cacheService.TryGetCached(text, voiceConfig, out var cacheFile))
            {
                _logger.LogDebug("Playing from cache: {Text} (source: {Source})", text, source ?? "default");

                // Check lock before playback
                if (_speechLockService.IsLocked)
                {
                    _queueService.Enqueue(text, source);
                    _logger.LogInformation("🎤 Lock detected before cache playback - queuing: {Text}", text);
                    return;
                }

                await _playbackService.PlayAsync(cacheFile, cancellationToken);
                return;
            }

            // Generate new audio via provider chain (with circuit breaker)
            _logger.LogDebug("Generating audio for: {Text} (source: {Source})", text, source ?? "default");
            var (audioData, providerUsed) = await _ttsProviderChain.SynthesizeAsync(text, voiceConfig, source, cancellationToken);

            if (audioData == null || audioData.Length == 0)
            {
                _logger.LogWarning("All TTS providers failed for: {Text}", text);
                return;
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
                return;
            }

            // Play audio
            await _playbackService.PlayAsync(audioFile, cancellationToken);

            _logger.LogInformation("🗣️ TTS [{Source}] via {Provider}: \"{Text}\"", source ?? "default", providerUsed, text);
        }
        finally
        {
            _playbackLock.Release();
        }
    }

    public void Dispose()
    {
        _playbackLock.Dispose();
    }
}
