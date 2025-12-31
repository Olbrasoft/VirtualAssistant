using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Audio;

/// <summary>
/// Coordinates audio recording workflow including buffer management and capture task lifecycle.
/// Implements Single Responsibility Principle - only handles audio recording coordination.
/// </summary>
public class AudioRecordingCoordinator : IAudioRecordingCoordinator
{
    private readonly ILogger<AudioRecordingCoordinator> _logger;
    private readonly IAudioCaptureService _audioCapture;

    private CancellationTokenSource? _recordingCts;
    private Task? _recordingTask;
    private List<byte> _audioBuffer = new();
    private readonly object _bufferLock = new();

    public AudioRecordingCoordinator(
        ILogger<AudioRecordingCoordinator> logger,
        IAudioCaptureService audioCapture)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _audioCapture = audioCapture ?? throw new ArgumentNullException(nameof(audioCapture));
    }

    /// <inheritdoc />
    public bool IsRecording => _recordingTask != null && !_recordingTask.IsCompleted;

    /// <inheritdoc />
    public async Task StartRecordingAsync(CancellationToken cancellationToken = default)
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
            await CleanupRecordingAsync();
            throw;
        }
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

            // Wait for recording task to complete
            if (_recordingTask != null)
            {
                try
                {
                    await _recordingTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected when canceling
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
            await CleanupRecordingAsync();
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

            // Wait for recording task to complete
            if (_recordingTask != null)
            {
                try
                {
                    await _recordingTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected
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
            await CleanupRecordingAsync();
        }
    }

    /// <summary>
    /// Captures audio chunks from the audio capture service and buffers them.
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
                    lock (_bufferLock)
                    {
                        _audioBuffer.AddRange(chunk);
                    }

                    _logger.LogDebug("Audio chunk captured: {Bytes} bytes (total: {Total})", chunk.Length, _audioBuffer.Count);
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
    private Task CleanupRecordingAsync()
    {
        _recordingCts?.Dispose();
        _recordingCts = null;
        _recordingTask = null;
        return Task.CompletedTask;
    }
}
