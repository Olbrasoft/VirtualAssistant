using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.StateMachine;

/// <summary>
/// Manages voice listener state transitions.
/// </summary>
public class VoiceStateMachine : IVoiceStateMachine
{
    private readonly ILogger<VoiceStateMachine> _logger;
    private readonly object _lock = new();

    private VoiceState _currentState;
    private DateTime _recordingStart;
    private DateTime _silenceStart;

    /// <inheritdoc />
    public VoiceState CurrentState
    {
        get { lock (_lock) return _currentState; }
    }

    /// <inheritdoc />
    public DateTime RecordingStartTime
    {
        get { lock (_lock) return _recordingStart; }
    }

    /// <inheritdoc />
    public DateTime SilenceStartTime
    {
        get { lock (_lock) return _silenceStart; }
        set { lock (_lock) _silenceStart = value; }
    }

    /// <inheritdoc />
    public event EventHandler<VoiceStateChangedEventArgs>? StateChanged;

    public VoiceStateMachine(ILogger<VoiceStateMachine> logger, bool startMuted = false)
    {
        _logger = logger;
        _currentState = startMuted ? VoiceState.Muted : VoiceState.Waiting;
        _logger.LogInformation("VoiceStateMachine initialized in {State} state", _currentState);
    }

    /// <inheritdoc />
    public void TransitionTo(VoiceState newState)
    {
        VoiceState previousState;
        lock (_lock)
        {
            if (_currentState == newState)
                return;

            previousState = _currentState;
            _currentState = newState;
        }

        _logger.LogDebug("State transition: {Previous} -> {New}", previousState, newState);
        StateChanged?.Invoke(this, new VoiceStateChangedEventArgs(previousState, newState));
    }

    /// <inheritdoc />
    public void StartRecording(float vadProbability)
    {
        lock (_lock)
        {
            _currentState = VoiceState.Recording;
            _recordingStart = DateTime.UtcNow;
            _silenceStart = default;
        }

        _logger.LogInformation("Recording started. VAD probability: {Probability:F2}", vadProbability);
        StateChanged?.Invoke(this, new VoiceStateChangedEventArgs(VoiceState.Waiting, VoiceState.Recording));
    }

    /// <inheritdoc />
    public void ResetToWaiting()
    {
        VoiceState previousState;
        lock (_lock)
        {
            previousState = _currentState;
            _currentState = VoiceState.Waiting;
            _silenceStart = default;
        }

        _logger.LogDebug("Reset to Waiting state");
        if (previousState != VoiceState.Waiting)
        {
            StateChanged?.Invoke(this, new VoiceStateChangedEventArgs(previousState, VoiceState.Waiting));
        }
    }

    /// <inheritdoc />
    public void ResetToMuted()
    {
        VoiceState previousState;
        lock (_lock)
        {
            previousState = _currentState;
            _currentState = VoiceState.Muted;
            _silenceStart = default;
            _recordingStart = default;
        }

        _logger.LogDebug("Reset to Muted state");
        if (previousState != VoiceState.Muted)
        {
            StateChanged?.Invoke(this, new VoiceStateChangedEventArgs(previousState, VoiceState.Muted));
        }
    }
}
