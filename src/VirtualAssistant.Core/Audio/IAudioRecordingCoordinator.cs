namespace Olbrasoft.VirtualAssistant.Core.Audio;

/// <summary>
/// Event args for a periodic audio chunk emitted during recording.
/// Contains a raw PCM slice since the previous chunk boundary and its ordinal index.
/// </summary>
public record AudioChunkEventArgs(int Index, byte[] PcmBytes);

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
    /// Fired periodically during recording with accumulated audio since the previous emit.
    /// Subscribed to only when streaming transcription is enabled. Not fired outside recording.
    /// </summary>
    event EventHandler<AudioChunkEventArgs>? ChunkAvailable;

    /// <summary>
    /// Enables periodic chunk emission on the current (or next) recording session.
    /// Call before <see cref="StartRecordingAsync"/> when streaming is active.
    /// </summary>
    /// <param name="interval">Minimum interval between chunk emits.</param>
    void EnableChunking(TimeSpan interval);

    /// <summary>
    /// Disables chunk emission. Already-emitted chunks keep their state in subscribers.
    /// </summary>
    void DisableChunking();

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
