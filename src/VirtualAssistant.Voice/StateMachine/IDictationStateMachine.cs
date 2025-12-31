using Olbrasoft.VirtualAssistant.Core.StateMachine;

namespace Olbrasoft.VirtualAssistant.Voice.StateMachine;

/// <summary>
/// Interface for managing dictation state transitions and validation.
/// </summary>
/// <remarks>
/// Implements State pattern for dictation workflow.
/// Valid state transitions:
/// - Idle → Recording (user starts dictation)
/// - Recording → Transcribing (recording stops normally)
/// - Recording → Idle (user cancels during recording - emergency stop)
/// - Transcribing → Idle (transcription completes or is cancelled)
/// </remarks>
public interface IDictationStateMachine
{
    /// <summary>
    /// Gets the current dictation state.
    /// </summary>
    DictationState CurrentState { get; }

    /// <summary>
    /// Event raised when state changes.
    /// </summary>
    event EventHandler<DictationState>? StateChanged;

    /// <summary>
    /// Checks if a state transition is valid.
    /// </summary>
    /// <param name="newState">The target state.</param>
    /// <returns>True if transition is valid, false otherwise.</returns>
    bool CanTransitionTo(DictationState newState);

    /// <summary>
    /// Transitions to a new state.
    /// </summary>
    /// <param name="newState">The target state.</param>
    /// <exception cref="InvalidOperationException">Thrown if transition is invalid.</exception>
    void TransitionTo(DictationState newState);
}
