using Olbrasoft.VirtualAssistant.Core.Audio;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <summary>
/// Audio-recording lifecycle lifted out of <c>DictationWorker</c> (#969
/// god-class split). Bundles <see cref="IAudioRecordingCoordinator"/> with
/// the streaming-session hooks on <see cref="IDictationTranscriber"/> so
/// the worker no longer subscribes to <c>ChunkAvailable</c>, toggles
/// chunking directly, or coordinates the transcriber's streaming state —
/// it just tells the session start / stop / cancel / reset. State-machine
/// transitions stay in the worker because they're cross-cutting across
/// the transcription, typing, and cancel paths too.
/// </summary>
public interface IDictationRecordingSession
{
    /// <summary>
    /// True when streaming chunk transcription is active for the current
    /// session (<see cref="StartAsync"/> was called with
    /// <c>streamingActive=true</c> and <see cref="EndSession"/> hasn't run
    /// yet). Delegates to the underlying transcriber.
    /// </summary>
    bool IsStreamingActive { get; }

    /// <summary>
    /// Start a new recording session. Opens the transcriber's streaming
    /// session, toggles chunking on the coordinator, and starts the audio
    /// capture. Caller owns state-machine transitions.
    /// </summary>
    Task StartAsync(bool streamingActive);

    /// <summary>
    /// Stop the recording and return the captured PCM buffer. Caller owns
    /// state-machine transitions and empty-buffer handling.
    /// </summary>
    Task<byte[]> StopAsync();

    /// <summary>
    /// Emergency-stop the recording (discards the buffer). Used on cancel,
    /// shutdown, and the "dictation disabled while active" path.
    /// </summary>
    Task EmergencyStopAsync();

    /// <summary>
    /// End the streaming session: cancels in-flight per-chunk transcription
    /// tasks and disables chunking on the coordinator. Safe to call
    /// repeatedly; cheap when streaming wasn't active.
    /// </summary>
    void EndSession();
}
