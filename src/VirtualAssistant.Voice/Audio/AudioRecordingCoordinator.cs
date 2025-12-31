using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Audio;

/// <summary>
/// Coordinates audio recording workflow including buffer management and capture task lifecycle.
/// Implements Single Responsibility Principle - only handles audio recording coordination.
/// </summary>
/// <remarks>
/// Maximum buffer size is configurable via AudioRecordingOptions.MaxBufferSizeBytes.
/// This prevents excessive memory usage during long recording sessions.
/// </remarks>
public class AudioRecordingCoordinator : IAudioRecordingCoordinator, IDisposable
{
    private readonly ILogger<AudioRecordingCoordinator> _logger;
    private readonly IAudioCaptureService _audioCapture;
    private readonly AudioRecordingOptions _options;

    private CancellationTokenSource? _recordingCts;
    private Task? _recordingTask;
    private readonly List<byte> _audioBuffer = new();
    private readonly object _bufferLock = new();
    private readonly object _startLock = new();
    private bool _disposed;

    public AudioRecordingCoordinator(
        ILogger<AudioRecordingCoordinator> logger,
        IAudioCaptureService audioCapture,
        IOptions<AudioRecordingOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _audioCapture = audioCapture ?? throw new ArgumentNullException(nameof(audioCapture));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public bool IsRecording => _recordingTask != null;

    /// <inheritdoc />
    public async Task StartRecordingAsync(CancellationToken cancellationToken = default)
    {
        // Use lock to prevent concurrent start operations (fixes race condition)
        lock (_startLock)
        {
            if (IsRecording)
            {
                _logger.LogWarning("Recording already in progress - ignoring start request");
                return;
            }

            try
            {
                // Clear audio buffer
                lock (_bufferLock)
                {
                    _audioBuffer.Clear();
                }

                // Create cancellation token for recording
                _recordingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                // Start audio capture
                _audioCapture.Start();

                // Start recording task to capture audio chunks
                _recordingTask = Task.Run(async () => await CaptureAudioAsync(_recordingCts.Token), _recordingCts.Token);

                _logger.LogInformation("Recording started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start recording");

                // Ensure audio capture is stopped if it was started
                try
                {
                    _audioCapture.Stop();
                }
                catch (Exception stopEx)
                {
                    _logger.LogWarning(stopEx, "Error stopping audio capture during cleanup");
                }

                CleanupRecording();
                throw;
            }
        }

        await Task.CompletedTask; // Prevent async warning since we're using lock
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
            // Cancel recording task
            _recordingCts?.Cancel();

            // Wait for recording task to complete (use caller's cancellation token for timeout control)
            if (_recordingTask != null)
            {
                try
                {
                    await _recordingTask.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when canceling recording or when caller cancels
                }
            }

            // Stop audio capture
            _audioCapture.Stop();

            // Get recorded audio
            byte[] audioData;
            lock (_bufferLock)
            {
                audioData = _audioBuffer.ToArray();
                _audioBuffer.Clear();
            }

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

            // Cancel recording task
            _recordingCts?.Cancel();

            // Wait for recording task to complete (use caller's cancellation token for timeout control)
            if (_recordingTask != null)
            {
                try
                {
                    await _recordingTask.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when canceling recording or when caller cancels
                }
            }

            // Stop audio capture
            _audioCapture.Stop();

            // Clear buffer
            lock (_bufferLock)
            {
                _audioBuffer.Clear();
            }

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
                if (chunk != null)
                {
                    int totalBytes;
                    lock (_bufferLock)
                    {
                        // Check buffer size limit before adding chunk
                        if (_audioBuffer.Count + chunk.Length > _options.MaxBufferSizeBytes)
                        {
                            _logger.LogWarning(
                                "Audio buffer size limit reached ({MaxSize} bytes). Stopping capture to prevent excessive memory usage.",
                                _options.MaxBufferSizeBytes);
                            break;
                        }

                        _audioBuffer.AddRange(chunk);
                        totalBytes = _audioBuffer.Count;
                    }

                    _logger.LogDebug("Audio chunk captured: {Bytes} bytes (total: {Total})", chunk.Length, totalBytes);
                }
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

    /// <summary>
    /// Cleans up recording resources (CTS and task references).
    /// </summary>
    private void CleanupRecording()
    {
        _recordingCts?.Dispose();
        _recordingCts = null;
        _recordingTask = null;
    }

    /// <summary>
    /// Disposes resources and stops any active recording.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // If recording is in progress, perform emergency stop
        if (IsRecording)
        {
            _logger.LogWarning("AudioRecordingCoordinator disposed while recording - performing emergency stop");
            try
            {
                // Synchronous emergency stop (can't await in Dispose)
                EmergencyStopAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during emergency stop in Dispose");
            }
        }

        // Cleanup any remaining CTS
        CleanupRecording();
    }
}
