namespace Olbrasoft.VirtualAssistant.Core.StateMachine;

/// <summary>
/// Event arguments for voice state changes.
/// </summary>
public class VoiceStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// The previous state before the transition.
    /// </summary>
    public VoiceState PreviousState { get; }

    /// <summary>
    /// The new state after the transition.
    /// </summary>
    public VoiceState NewState { get; }

    /// <summary>
    /// Creates a new instance of VoiceStateChangedEventArgs.
    /// </summary>
    public VoiceStateChangedEventArgs(VoiceState previousState, VoiceState newState)
    {
        PreviousState = previousState;
        NewState = newState;
    }
}
