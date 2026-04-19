using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Core.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <summary>
/// Audio-recording lifecycle lifted out of <c>DictationWorker</c> (#969
/// god-class split). Bundles <see cref="IAudioRecordingCoordinator"/>,
/// the streaming-session hooks on <see cref="IDictationTranscriber"/>, and
/// the dictation state-machine transitions tied to recording so the worker
/// no longer holds a direct <c>IDictationStateMachine</c> dep or subscribes
/// to <c>ChunkAvailable</c>. It just tells the session start / stop / cancel
/// and reads <see cref="CurrentState"/> when it needs to guard commands.
/// </summary>
public interface IDictationRecordingSession
{
    /// <summary>
    /// Current dictation state (Idle / Recording / Transcribing). Delegates
    /// to the underlying <see cref="IDictationStateMachine"/> so consumers
    /// can guard commands without taking the state machine as a dep
    /// themselves. (#969 consolidation.)
    /// </summary>
    DictationState CurrentState { get; }

    /// <summary>
    /// True when streaming chunk transcription is active for the current
    /// session (<see cref="StartAsync"/> was called with
    /// <c>streamingActive=true</c> and <see cref="EndSession"/> hasn't run
    /// yet). Delegates to the underlying transcriber.
    /// </summary>
    bool IsStreamingActive { get; }

    /// <summary>
    /// Start a new recording session. Transitions the state machine to
    /// <see cref="DictationState.Recording"/>, opens the transcriber's
    /// streaming session, toggles chunking on the coordinator, and starts
    /// the audio capture. Failures are caught and logged internally — the
    /// session cleans up any partial side-effects and transitions back to
    /// Idle, so this method does not throw.
    /// </summary>
    Task StartAsync(bool streamingActive);

    /// <summary>
    /// Stop the recording and return the captured PCM buffer along with
    /// the state-machine transition it deserves: <c>null</c> when the
    /// buffer is empty (caller bails + state goes Idle), non-null when
    /// audio is good (state goes Transcribing).
    /// </summary>
    Task<byte[]?> StopAndValidateAsync();

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
