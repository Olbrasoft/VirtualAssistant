namespace Olbrasoft.VirtualAssistant.Core.Audio;

/// <summary>
/// Coordinates audio recording workflow including buffer management and capture task lifecycle.
/// Separates audio recording concerns from dictation state machine orchestration.
/// </summary>
public interface IAudioRecordingCoordinator
{
    /// <summary>
    /// Gets whether recording is currently in progress.
    /// </summary>
    bool IsRecording { get; }

    /// <summary>
    /// Starts audio recording, initializing buffer and capture task.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the recording session</param>
    /// <returns>Task representing the start operation</returns>
    Task StartRecordingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops audio recording and returns the captured audio data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Captured audio data as byte array, or empty array if no data was captured</returns>
    Task<byte[]> StopRecordingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Emergency stop of recording, discarding all captured data.
    /// Used when user cancels during recording or error occurs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the emergency stop operation</returns>
    Task EmergencyStopAsync(CancellationToken cancellationToken = default);
}
