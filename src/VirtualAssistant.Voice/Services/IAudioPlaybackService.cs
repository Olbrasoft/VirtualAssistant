using Microsoft.Extensions.Logging;
using Olbrasoft.NotificationAudio.Abstractions;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Service for audio playback control.
/// Handles playing audio files and stopping playback when needed.
/// </summary>
public interface IAudioPlaybackService
{
    /// <summary>
    /// Plays an audio file asynchronously.
    /// Monitors speech lock during playback and stops if lock is acquired.
    /// </summary>
    /// <param name="audioFile">Path to the audio file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PlayAsync(string audioFile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Plays audio from a stream asynchronously.
    /// Monitors speech lock during playback and stops if lock is acquired.
    /// </summary>
    /// <param name="audioStream">Audio data stream</param>
    /// <param name="fileExtension">File extension hint (e.g., ".mp3", ".wav")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PlayAsync(Stream audioStream, string fileExtension = ".mp3", CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops any currently playing audio immediately.
    /// </summary>
    void Stop();

    /// <summary>
    /// Gets whether audio is currently playing.
    /// </summary>
    bool IsPlaying { get; }
}

/// <summary>
/// Audio playback service using NotificationAudio library.
/// Monitors speech lock during playback and automatically stops if lock is acquired.
/// </summary>
public sealed class AudioPlaybackService : IAudioPlaybackService, IDisposable
{
    private readonly ILogger<AudioPlaybackService> _logger;
    private readonly ISpeechLockService _speechLockService;
    private readonly INotificationPlayer _player;
    private Task? _playbackTask;
    private readonly object _playbackLock = new();
    private CancellationTokenSource? _playbackCts;

    public AudioPlaybackService(
        INotificationPlayer player,
        ILogger<AudioPlaybackService> logger,
        ISpeechLockService speechLockService)
    {
        _player = player;
        _logger = logger;
        _speechLockService = speechLockService;
    }

    /// <inheritdoc />
    public bool IsPlaying
    {
        get
        {
            lock (_playbackLock)
            {
                return _playbackTask != null && !_playbackTask.IsCompleted;
            }
        }
    }

    /// <inheritdoc />
    public async Task PlayAsync(string audioFile, CancellationToken cancellationToken = default)
    {
        // Create cancellation token source for playback control
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        lock (_playbackLock)
        {
            _playbackCts = cts;
        }

        try
        {
            // Start playback in background task
            var playbackTask = Task.Run(async () =>
            {
                try
                {
                    await _player.PlayAsync(audioFile, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected when stopped
                }
            }, cts.Token);

            lock (_playbackLock)
            {
                _playbackTask = playbackTask;
            }

            // Monitor for speech lock while playing - check every 50ms
            while (!playbackTask.IsCompleted)
            {
                // Check if user started recording (speech lock acquired)
                if (_speechLockService.IsLocked)
                {
                    _logger.LogInformation("🛑 Speech lock detected during playback - stopping");
                    _player.Stop();
                    cts.Cancel();
                    break;
                }

                // Wait 50ms before next check, or until playback completes
                try
                {
                    await Task.Delay(50, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _player.Stop();
                    cts.Cancel();
                    throw;
                }
            }

            // Wait for playback to complete
            await playbackTask;
        }
        finally
        {
            lock (_playbackLock)
            {
                _playbackTask = null;
                _playbackCts = null;
            }
            cts.Dispose();
        }
    }

    /// <inheritdoc />
    public async Task PlayAsync(Stream audioStream, string fileExtension = ".mp3", CancellationToken cancellationToken = default)
    {
        // Create cancellation token source for playback control
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        lock (_playbackLock)
        {
            _playbackCts = cts;
        }

        try
        {
            // Start playback in background task
            var playbackTask = Task.Run(async () =>
            {
                try
                {
                    await _player.PlayAsync(audioStream, fileExtension, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Expected when stopped
                }
            }, cts.Token);

            lock (_playbackLock)
            {
                _playbackTask = playbackTask;
            }

            // Monitor for speech lock while playing - check every 50ms
            while (!playbackTask.IsCompleted)
            {
                // Check if user started recording (speech lock acquired)
                if (_speechLockService.IsLocked)
                {
                    _logger.LogInformation("🛑 Speech lock detected during stream playback - stopping");
                    _player.Stop();
                    cts.Cancel();
                    break;
                }

                // Wait 50ms before next check, or until playback completes
                try
                {
                    await Task.Delay(50, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _player.Stop();
                    cts.Cancel();
                    throw;
                }
            }

            // Wait for playback to complete
            await playbackTask;
        }
        finally
        {
            lock (_playbackLock)
            {
                _playbackTask = null;
                _playbackCts = null;
            }
            cts.Dispose();
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        try
        {
            CancellationTokenSource? cts;
            lock (_playbackLock)
            {
                cts = _playbackCts;
            }

            if (cts != null && !cts.IsCancellationRequested)
            {
                _logger.LogInformation("🛑 Stopping audio playback");
                _player.Stop();
                cts.Cancel();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping audio playback");
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
