namespace Olbrasoft.VirtualAssistant.Core.StateMachine;

/// <summary>
/// Represents the current state of the voice listener.
/// </summary>
public enum VoiceState
{
    /// <summary>
    /// Waiting for speech to begin.
    /// </summary>
    Waiting,

    /// <summary>
    /// Currently recording speech.
    /// </summary>
    Recording,

    /// <summary>
    /// Microphone is muted, not listening.
    /// </summary>
    Muted
}
