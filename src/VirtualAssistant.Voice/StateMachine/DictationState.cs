namespace Olbrasoft.VirtualAssistant.Voice.StateMachine;

/// <summary>
/// Dictation states for the application.
/// </summary>
public enum DictationState
{
    /// <summary>
    /// Idle state - waiting for user to start dictation.
    /// </summary>
    Idle,

    /// <summary>
    /// Recording state - actively capturing audio.
    /// </summary>
    Recording,

    /// <summary>
    /// Transcribing state - processing audio through Whisper and LLM.
    /// </summary>
    Transcribing
}
