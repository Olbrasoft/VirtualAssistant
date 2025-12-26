namespace Olbrasoft.VirtualAssistant.Core.StateMachine;

/// <summary>
/// Manages voice listener state transitions.
/// </summary>
public interface IVoiceStateMachine
{
    /// <summary>
    /// Gets the current voice state.
    /// </summary>
    VoiceState CurrentState { get; }

    /// <summary>
    /// Transitions to the specified state.
    /// </summary>
    void TransitionTo(VoiceState newState);

    /// <summary>
    /// Resets the state machine to waiting state and clears timing data.
    /// </summary>
    void ResetToWaiting();

    /// <summary>
    /// Resets the state machine to muted state and clears all data.
    /// </summary>
    void ResetToMuted();

    /// <summary>
    /// Starts recording with the given VAD probability.
    /// </summary>
    void StartRecording(float vadProbability);

    /// <summary>
    /// Gets the time when recording started.
    /// </summary>
    DateTime RecordingStartTime { get; }

    /// <summary>
    /// Gets or sets the time when silence started during recording.
    /// </summary>
    DateTime SilenceStartTime { get; set; }

    /// <summary>
    /// Event raised when the state changes.
    /// </summary>
    event EventHandler<VoiceStateChangedEventArgs>? StateChanged;
}
