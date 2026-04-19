using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Service.Hubs;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <inheritdoc />
public sealed class DictationStateBroadcaster : IDictationStateBroadcaster, IDisposable
{
    private readonly IDictationStateMachine _stateMachine;
    private readonly IDictationTranscriber _transcriber;
    private readonly IDictationOutputChannel _outputChannel;
    private Action<EventHandler<string>>? _unsubscribeCompleted;
    private EventHandler<string>? _completedHandler;
    private bool _subscribed;

    public DictationStateBroadcaster(
        IDictationStateMachine stateMachine,
        IDictationTranscriber transcriber,
        IDictationOutputChannel outputChannel)
    {
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _transcriber = transcriber ?? throw new ArgumentNullException(nameof(transcriber));
        _outputChannel = outputChannel ?? throw new ArgumentNullException(nameof(outputChannel));
    }

    public void Start(
        Action<EventHandler<string>> subscribeTranscriptionCompleted,
        Action<EventHandler<string>> unsubscribeTranscriptionCompleted)
    {
        ArgumentNullException.ThrowIfNull(subscribeTranscriptionCompleted);
        ArgumentNullException.ThrowIfNull(unsubscribeTranscriptionCompleted);

        if (_subscribed) return;

        _stateMachine.StateChanged += OnStateChanged;
        _transcriber.RawTranscriptionReady += OnRawTranscriptionReady;

        _completedHandler = OnTranscriptionCompleted;
        subscribeTranscriptionCompleted(_completedHandler);
        _unsubscribeCompleted = unsubscribeTranscriptionCompleted;

        _subscribed = true;
    }

    public void Stop()
    {
        if (!_subscribed) return;

        _stateMachine.StateChanged -= OnStateChanged;
        _transcriber.RawTranscriptionReady -= OnRawTranscriptionReady;

        if (_completedHandler is not null)
        {
            _unsubscribeCompleted?.Invoke(_completedHandler);
            _completedHandler = null;
        }
        _unsubscribeCompleted = null;

        _subscribed = false;
    }

    public void Dispose() => Stop();

    private void OnStateChanged(object? sender, DictationState state)
    {
        var eventType = state switch
        {
            DictationState.Recording => DictationEventType.RecordingStarted,
            DictationState.Transcribing => DictationEventType.TranscriptionStarted,
            _ => DictationEventType.RecordingStopped
        };

        _ = _outputChannel.BroadcastEventAsync(eventType, null);
    }

    private void OnTranscriptionCompleted(object? sender, string text) =>
        _ = _outputChannel.BroadcastEventAsync(DictationEventType.TranscriptionCompleted, text);

    private void OnRawTranscriptionReady(string text) =>
        _ = _outputChannel.BroadcastEventAsync(DictationEventType.RawTranscriptionCompleted, text);
}
