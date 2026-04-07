using Olbrasoft.VirtualAssistant.Core.StateMachine;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Service for controlling dictation from multiple sources (keyboard, web remote).
/// </summary>
public interface IDictationService
{
    /// <summary>
    /// Gets the current dictation state.
    /// </summary>
    DictationState State { get; }

    /// <summary>
    /// Starts dictation recording.
    /// </summary>
    Task StartDictationAsync();

    /// <summary>
    /// Starts quick dictation recording (raw STT only, no LLM, auto-paste + auto-Enter).
    /// </summary>
    Task StartQuickDictationAsync();

    /// <summary>
    /// Stops recording and starts transcription using the mode that was
    /// chosen at start time (StartDictationAsync vs StartQuickDictationAsync).
    /// </summary>
    Task StopDictationAsync();

    /// <summary>
    /// Stops recording and starts transcription, overriding the start-time
    /// mode with the supplied <paramref name="quickMode"/>. This allows the
    /// caller to defer the choice of fast vs LLM-corrected processing until
    /// the moment recording stops — e.g. the Remote Control single dictation
    /// button shows two release zones during recording and picks the mode
    /// based on which zone the user releases on.
    /// </summary>
    /// <param name="quickMode">true = quick (raw STT, auto-paste + Enter),
    /// false = normal (LLM correction).</param>
    Task StopDictationAsync(bool quickMode);

    /// <summary>
    /// Cancels ongoing transcription.
    /// </summary>
    void CancelTranscription();

    /// <summary>
    /// Raised when transcription completes with the transcribed text.
    /// </summary>
    event EventHandler<string>? TranscriptionCompleted;
}
