using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Service.Hubs;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <summary>
/// SignalR broadcast bridge for dictation events. Subscribes to the state
/// machine's <c>StateChanged</c>, the transcriber's <c>RawTranscriptionReady</c>,
/// and the worker's public <c>TranscriptionCompleted</c> (via
/// <see cref="IDictationService"/>) and fans every emission into
/// <see cref="IDictationOutputChannel.BroadcastEventAsync"/>.
///
/// Runs as a standalone <see cref="BackgroundService"/> rather than being
/// injected into <c>DictationWorker</c> so the worker's ctor-dep count stays
/// low — the broadcaster is self-starting off the worker singleton it pulls
/// via <see cref="IDictationService"/>. (#969 god-class split.)
/// </summary>
public sealed class DictationStateBroadcaster : BackgroundService
{
    private readonly IDictationStateMachine _stateMachine;
    private readonly IDictationTranscriber _transcriber;
    private readonly IDictationOutputChannel _outputChannel;
    private readonly IDictationService _dictationService;

    public DictationStateBroadcaster(
        IDictationStateMachine stateMachine,
        IDictationTranscriber transcriber,
        IDictationOutputChannel outputChannel,
        IDictationService dictationService)
    {
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _transcriber = transcriber ?? throw new ArgumentNullException(nameof(transcriber));
        _outputChannel = outputChannel ?? throw new ArgumentNullException(nameof(outputChannel));
        _dictationService = dictationService ?? throw new ArgumentNullException(nameof(dictationService));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stateMachine.StateChanged += OnStateChanged;
        _transcriber.RawTranscriptionReady += OnRawTranscriptionReady;
        _dictationService.TranscriptionCompleted += OnTranscriptionCompleted;

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        finally
        {
            _stateMachine.StateChanged -= OnStateChanged;
            _transcriber.RawTranscriptionReady -= OnRawTranscriptionReady;
            _dictationService.TranscriptionCompleted -= OnTranscriptionCompleted;
        }
    }

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
