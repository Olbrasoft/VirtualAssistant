using Olbrasoft.VirtualAssistant.Core.Speech;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <summary>
/// Post-recording "audio → spoken-into-active-window" pipeline lifted out of
/// <c>DictationWorker</c> (#969 god-class split). Bundles the transcriber
/// call, the typing-sound feedback, the persister save, the Claude-Code
/// civility trim, and the FastPaste / TypeIntoActiveWindow + state-machine
/// transitions that used to live inline in the worker's
/// <c>StopAndTranscribeAsync</c> body. The worker keeps just the recording
/// lifecycle, the CTS lifecycle, and the top-level exception handling.
/// </summary>
public interface IDictationCompletionPipeline
{
    /// <summary>
    /// Quick-mode completion: raw (no-LLM) transcription, civility trim for
    /// Claude Code, fast paste without clipboard save/restore, and the
    /// <c>QuickTranscriptionCompleted</c> broadcast. Owns the typing sound
    /// feedback and the Idle transition on every exit path.
    /// </summary>
    Task CompleteQuickAsync(byte[] audio, CancellationToken cancellationToken);

    /// <summary>
    /// Full-mode completion: full-pipeline transcription (LLM correction,
    /// filters, racing), persister save, <paramref name="onTranscriptionReady"/>
    /// invoked before typing so remote UI updates immediately, then
    /// TypeIntoActiveWindow + Idle transition.
    /// </summary>
    /// <param name="audio">PCM buffer from the recording session.</param>
    /// <param name="onTranscriptionReady">
    /// Callback raised with the final text between transcription completing
    /// and typing starting. Worker uses this to re-raise its public
    /// <c>TranscriptionCompleted</c> event so the SignalR broadcast lands
    /// before the keystrokes do.
    /// </param>
    /// <param name="cancellationToken">Cancellation from the worker's CTS.</param>
    Task CompleteFullAsync(
        byte[] audio,
        Action<string> onTranscriptionReady,
        CancellationToken cancellationToken);
}
