using Olbrasoft.VirtualAssistant.Core.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <summary>
/// Cancel / emergency-stop orchestration lifted out of <c>DictationWorker</c>
/// (#969 god-class split). Centralises the four teardown paths that all
/// combine "stop recording + transition to Idle" with small variations
/// (whether to play the cancel cue, stop typing feedback, or cancel the
/// in-flight transcription CTS).
/// </summary>
public interface IDictationCancellationCoordinator
{
    /// <summary>
    /// Begin a new transcription session: creates the cancellation token
    /// source and returns its token. The token is what the worker feeds to
    /// <c>IDictationCompletionPipeline</c> so user-cancel (via
    /// <see cref="CancelTranscription"/>) bubbles into the pipeline.
    /// </summary>
    CancellationToken BeginTranscription();

    /// <summary>
    /// Dispose the current transcription CTS. Safe to call when no
    /// transcription is in flight — used from the worker's
    /// <c>StopAndTranscribeAsync</c> finally.
    /// </summary>
    void EndTranscription();

    /// <summary>
    /// Pause-while-recording path: emergency-stops the recording session
    /// (discards audio), plays the paper-rip cancel cue, and transitions
    /// the state machine to Idle.
    /// </summary>
    Task CancelRecordingAsync();

    /// <summary>
    /// Dictation-disabled-while-active path: emergency-stops the recording
    /// session and transitions to Idle; resets streaming state in finally.
    /// No cancel cue (this is a programmatic disable, not a user cancel).
    /// </summary>
    Task EmergencyStopAsync();

    /// <summary>
    /// User-initiated cancel (Pause during Transcribing, remote Cancel):
    /// stops typing feedback, plays the cancel cue, emergency-stops the
    /// recording if it's still running, cancels + disposes the transcription
    /// CTS, resets streaming state, and transitions to Idle.
    /// </summary>
    void CancelTranscription();

    /// <summary>
    /// Worker-shutdown path: emergency-stops the recording and transitions
    /// to Idle. No sound, no session reset — the process is about to exit.
    /// </summary>
    Task ShutdownAsync();
}
