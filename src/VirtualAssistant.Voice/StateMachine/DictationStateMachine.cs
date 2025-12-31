using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.StateMachine;

namespace Olbrasoft.VirtualAssistant.Voice.StateMachine;

/// <summary>
/// State machine for managing dictation state transitions.
/// </summary>
/// <remarks>
/// Implements State pattern (GoF) for dictation workflow.
/// Thread-safe: All state transitions are protected by a lock.
///
/// Valid transitions:
/// - Idle → Recording: User starts dictation (CapsLock ON)
/// - Recording → Transcribing: Recording stops normally (CapsLock OFF)
/// - Recording → Idle: Emergency stop (user toggles CapsLock ON again during recording)
/// - Transcribing → Idle: Transcription completes or user cancels (Escape)
///
/// Invalid transitions (will throw):
/// - Recording → Recording: Already recording
/// - Transcribing → Recording: Cannot start new recording while transcribing
/// - Transcribing → Transcribing: Already transcribing
/// - Idle → Transcribing: Cannot transcribe without recording first
/// </remarks>
public class DictationStateMachine : IDictationStateMachine
{
    private readonly ILogger<DictationStateMachine> _logger;
    private readonly object _lock = new();
    private DictationState _currentState = DictationState.Idle;

    /// <inheritdoc/>
    public DictationState CurrentState
    {
        get
        {
            lock (_lock)
            {
                return _currentState;
            }
        }
    }

    /// <inheritdoc/>
    public event EventHandler<DictationState>? StateChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="DictationStateMachine"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public DictationStateMachine(ILogger<DictationStateMachine> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public bool CanTransitionTo(DictationState newState)
    {
        lock (_lock)
        {
            // Transitioning to the same state is allowed (idempotent)
            if (_currentState == newState)
                return true;

            return (_currentState, newState) switch
            {
                // Valid transitions
                (DictationState.Idle, DictationState.Recording) => true,           // Start recording
                (DictationState.Recording, DictationState.Transcribing) => true,   // Normal stop
                (DictationState.Recording, DictationState.Idle) => true,           // Emergency cancel
                (DictationState.Transcribing, DictationState.Idle) => true,        // Transcription done

                // All other transitions are invalid
                _ => false
            };
        }
    }

    /// <inheritdoc/>
    public void TransitionTo(DictationState newState)
    {
        lock (_lock)
        {
            // Idempotent: transitioning to same state is allowed (no-op)
            if (_currentState == newState)
            {
                _logger.LogDebug("Already in state {State}, ignoring transition", newState);
                return;
            }

            if (!CanTransitionTo(newState))
            {
                var message = $"Invalid state transition: {_currentState} → {newState}";
                _logger.LogError(message);
                throw new InvalidOperationException(message);
            }

            var oldState = _currentState;
            _currentState = newState;

            _logger.LogInformation("State transition: {OldState} → {NewState}", oldState, newState);

            // Raise event outside lock to prevent deadlocks
            var stateChanged = StateChanged;
            if (stateChanged != null)
            {
                // Fire event synchronously to ensure proper ordering
                stateChanged.Invoke(this, newState);
            }
        }
    }
}
