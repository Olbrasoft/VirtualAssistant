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
    /// Stops recording and starts transcription.
    /// </summary>
    Task StopDictationAsync();

    /// <summary>
    /// Cancels ongoing transcription.
    /// </summary>
    void CancelTranscription();

    /// <summary>
    /// Raised when transcription completes with the transcribed text.
    /// </summary>
    event EventHandler<string>? TranscriptionCompleted;
}
