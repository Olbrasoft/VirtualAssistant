using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Audio;

/// <summary>
/// Coordinates audio recording workflow including buffer management and capture task lifecycle.
/// Buffer state and chunk-emission timing are delegated to <see cref="IAudioBufferManager"/>
/// and <see cref="IChunkEmissionScheduler"/>; this class owns only the recording-session
/// lifecycle (start/stop/emergency-stop, capture task, cancellation).
/// </summary>
/// <remarks>
/// Maximum buffer size is configurable via <see cref="AudioRecordingOptions.MaxBufferSizeBytes"/>.
/// This prevents excessive memory usage during long recording sessions.
/// </remarks>
public class AudioRecordingCoordinator : IAudioRecordingCoordinator, IDisposable
{
    private readonly ILogger<AudioRecordingCoordinator> _logger;
    private readonly IAudioCaptureService _audioCapture;
    private readonly AudioRecordingOptions _options;
    private readonly IAudioBufferManager _buffer;
    private readonly IChunkEmissionScheduler _scheduler;

    private CancellationTokenSource? _recordingCts;
    private Task? _recordingTask;
    private readonly object _startLock = new();
    private bool _disposed;

    public AudioRecordingCoordinator(
        ILogger<AudioRecordingCoordinator> logger,
        IAudioCaptureService audioCapture,
        IOptions<AudioRecordingOptions> options,
        IAudioBufferManager buffer,
        IChunkEmissionScheduler scheduler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _audioCapture = audioCapture ?? throw new ArgumentNullException(nameof(audioCapture));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));

        // Re-raise the scheduler's event from this instance so the sender the
        // subscribers see is the coordinator, matching the pre-split contract
        // where AudioRecordingCoordinator was the emitter. (Copilot review on
        // PR #1001.)
        _scheduler.ChunkAvailable += OnSchedulerChunkAvailable;
    }

    private void OnSchedulerChunkAvailable(object? sender, AudioChunkEventArgs e)
        => ChunkAvailable?.Invoke(this, e);

    /// <inheritdoc />
    public bool IsRecording => _recordingTask != null;

    /// <inheritdoc />
    public event EventHandler<AudioChunkEventArgs>? ChunkAvailable;

    /// <inheritdoc />
    public void EnableChunking(TimeSpan interval) => _scheduler.Enable(interval);

    /// <inheritdoc />
    public void DisableChunking() => _scheduler.Disable();

    /// <inheritdoc />
    public async Task StartRecordingAsync(CancellationToken cancellationToken = default)
    {
        lock (_startLock)
        {
            if (IsRecording)
            {
                _logger.LogWarning("Recording already in progress - ignoring start request");
                return;
            }

            try
            {
                _buffer.Clear();
                _scheduler.Reset();

                _recordingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _audioCapture.Start();
                _recordingTask = Task.Run(async () => await CaptureAudioAsync(_recordingCts.Token), _recordingCts.Token);

                _logger.LogInformation("Recording started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start recording");
                try { _audioCapture.Stop(); }
                catch (Exception stopEx) { _logger.LogWarning(stopEx, "Error stopping audio capture during cleanup"); }
                CleanupRecording();
                throw;
            }
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<byte[]> StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRecording)
        {
            _logger.LogWarning("No active recording to stop");
            return Array.Empty<byte>();
        }

        try
        {
            _recordingCts?.Cancel();

            if (_recordingTask != null)
            {
                try { await _recordingTask.WaitAsync(cancellationToken); }
                catch (OperationCanceledException) { /* expected */ }
            }

            _audioCapture.Stop();

            // Drain the tail as a final chunk (no-op when chunking disabled or empty)
            // before we empty the buffer for the caller.
            _scheduler.EmitFinal(_buffer);

            var audioData = _buffer.DrainToArray();
            _logger.LogInformation("Recording stopped - {Bytes} bytes captured", audioData.Length);
            return audioData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping recording");
            throw;
        }
        finally
        {
            CleanupRecording();
        }
    }

    /// <inheritdoc />
    public async Task EmergencyStopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRecording)
        {
            _logger.LogDebug("No active recording - emergency stop not needed");
            return;
        }

        try
        {
            _logger.LogWarning("Emergency stop triggered");
            _recordingCts?.Cancel();

            if (_recordingTask != null)
            {
                try { await _recordingTask.WaitAsync(cancellationToken); }
                catch (OperationCanceledException) { /* expected */ }
            }

            _audioCapture.Stop();
            _buffer.Clear();

            _logger.LogInformation("Emergency stop completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during emergency stop");
        }
        finally
        {
            CleanupRecording();
        }
    }

    /// <summary>
    /// Captures audio chunks from the audio capture service and buffers them.
    /// Stops automatically if buffer exceeds configured maximum buffer size.
    /// </summary>
    private async Task CaptureAudioAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var chunk = await _audioCapture.ReadChunkAsync(cancellationToken);
                if (chunk == null) continue;

                if (!_buffer.TryAppend(chunk, _options.MaxBufferSizeBytes, out var totalBytes))
                {
                    _logger.LogWarning(
                        "Audio buffer size limit reached ({MaxSize} bytes). Stopping capture to prevent excessive memory usage.",
                        _options.MaxBufferSizeBytes);
                    break;
                }

                _logger.LogDebug("Audio chunk captured: {Bytes} bytes (total: {Total})", chunk.Length, totalBytes);
                _scheduler.TryEmitIfDue(_buffer);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Audio capture canceled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing audio");
        }
    }

    private void CleanupRecording()
    {
        _recordingCts?.Dispose();
        _recordingCts = null;
        _recordingTask = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _scheduler.ChunkAvailable -= OnSchedulerChunkAvailable;

        if (IsRecording)
        {
            _logger.LogWarning("AudioRecordingCoordinator disposed while recording - performing emergency stop");
            try
            {
                // Synchronous emergency stop (can't await in Dispose).
                EmergencyStopAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during emergency stop in Dispose");
            }
        }

        CleanupRecording();
    }
}
