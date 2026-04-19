using Moq;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Service.Hubs;
using Olbrasoft.VirtualAssistant.Service.Workers.Dictation;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Workers.Dictation;

/// <summary>
/// Pins the SignalR fan-out BackgroundService. Subscribes to state machine's
/// StateChanged, transcriber's RawTranscriptionReady, and the worker's
/// TranscriptionCompleted (via IDictationService) and maps each emission to
/// the matching DictationEventType broadcast. Runs as a standalone hosted
/// service so DictationWorker doesn't need to inject or manage it.
/// </summary>
public class DictationStateBroadcasterTests : IDisposable
{
    private readonly Mock<IDictationStateMachine> _stateMachineMock = new();
    private readonly Mock<IDictationTranscriber> _transcriberMock = new();
    private readonly Mock<IDictationOutputChannel> _outputChannelMock = new();
    private readonly Mock<IDictationService> _dictationServiceMock = new();

    private readonly DictationStateBroadcaster _sut;
    private readonly CancellationTokenSource _cts = new();

    public DictationStateBroadcasterTests()
    {
        _sut = new DictationStateBroadcaster(
            _stateMachineMock.Object,
            _transcriberMock.Object,
            _outputChannelMock.Object,
            _dictationServiceMock.Object);
    }

    /// <summary>
    /// Starts the broadcaster and waits deterministically until ExecuteAsync
    /// has wired up its three event subscriptions. Uses SetupAdd callbacks
    /// to decrement a counter — once all three fire, a TaskCompletionSource
    /// flips and the test proceeds. Replaces a fragile Task.Delay(20) wait.
    /// (Copilot review on PR #1037.)
    /// </summary>
    private async Task StartAsync()
    {
        var subscriptionsRegistered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var remainingSubscriptions = 3;

        void MarkSubscribed()
        {
            if (Interlocked.Decrement(ref remainingSubscriptions) == 0)
                subscriptionsRegistered.TrySetResult();
        }

        _stateMachineMock
            .SetupAdd(x => x.StateChanged += It.IsAny<EventHandler<DictationState>>())
            .Callback(MarkSubscribed);

        _transcriberMock
            .SetupAdd(x => x.RawTranscriptionReady += It.IsAny<Action<string>>())
            .Callback(MarkSubscribed);

        _dictationServiceMock
            .SetupAdd(x => x.TranscriptionCompleted += It.IsAny<EventHandler<string>>())
            .Callback(MarkSubscribed);

        _ = _sut.StartAsync(_cts.Token);
        await subscriptionsRegistered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ExecuteAsync_SubscribesToAllEvents()
    {
        await StartAsync();

        _stateMachineMock.VerifyAdd(x => x.StateChanged += It.IsAny<EventHandler<DictationState>>(), Times.Once);
        _transcriberMock.VerifyAdd(x => x.RawTranscriptionReady += It.IsAny<Action<string>>(), Times.Once);
        _dictationServiceMock.VerifyAdd(x => x.TranscriptionCompleted += It.IsAny<EventHandler<string>>(), Times.Once);
    }

    [Fact]
    public async Task StopAsync_UnsubscribesFromAllEvents()
    {
        await StartAsync();

        await _sut.StopAsync(CancellationToken.None);

        _stateMachineMock.VerifyRemove(x => x.StateChanged -= It.IsAny<EventHandler<DictationState>>(), Times.Once);
        _transcriberMock.VerifyRemove(x => x.RawTranscriptionReady -= It.IsAny<Action<string>>(), Times.Once);
        _dictationServiceMock.VerifyRemove(x => x.TranscriptionCompleted -= It.IsAny<EventHandler<string>>(), Times.Once);
    }

    [Fact]
    public async Task StateChanged_Recording_BroadcastsRecordingStarted()
    {
        await StartAsync();

        _stateMachineMock.Raise(x => x.StateChanged += null, this, DictationState.Recording);

        _outputChannelMock.Verify(
            x => x.BroadcastEventAsync(DictationEventType.RecordingStarted, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StateChanged_Transcribing_BroadcastsTranscriptionStarted()
    {
        await StartAsync();

        _stateMachineMock.Raise(x => x.StateChanged += null, this, DictationState.Transcribing);

        _outputChannelMock.Verify(
            x => x.BroadcastEventAsync(DictationEventType.TranscriptionStarted, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StateChanged_Idle_BroadcastsRecordingStopped()
    {
        await StartAsync();

        _stateMachineMock.Raise(x => x.StateChanged += null, this, DictationState.Idle);

        _outputChannelMock.Verify(
            x => x.BroadcastEventAsync(DictationEventType.RecordingStopped, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RawTranscriptionReady_BroadcastsRawTranscriptionCompleted()
    {
        await StartAsync();

        _transcriberMock.Raise(x => x.RawTranscriptionReady += null, "raw text");

        _outputChannelMock.Verify(
            x => x.BroadcastEventAsync(DictationEventType.RawTranscriptionCompleted, "raw text", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TranscriptionCompleted_BroadcastsTranscriptionCompleted()
    {
        await StartAsync();

        _dictationServiceMock.Raise(x => x.TranscriptionCompleted += null, this, "final text");

        _outputChannelMock.Verify(
            x => x.BroadcastEventAsync(DictationEventType.TranscriptionCompleted, "final text", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Stop_UnsubscribesTranscriptionCompletedToo()
    {
        await StartAsync();
        await _sut.StopAsync(CancellationToken.None);

        _dictationServiceMock.Raise(x => x.TranscriptionCompleted += null, this, "after stop");

        _outputChannelMock.Verify(
            x => x.BroadcastEventAsync(It.IsAny<DictationEventType>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Constructor_NullStateMachine_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DictationStateBroadcaster(null!, _transcriberMock.Object, _outputChannelMock.Object, _dictationServiceMock.Object));

    [Fact]
    public void Constructor_NullTranscriber_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DictationStateBroadcaster(_stateMachineMock.Object, null!, _outputChannelMock.Object, _dictationServiceMock.Object));

    [Fact]
    public void Constructor_NullOutputChannel_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DictationStateBroadcaster(_stateMachineMock.Object, _transcriberMock.Object, null!, _dictationServiceMock.Object));

    [Fact]
    public void Constructor_NullDictationService_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DictationStateBroadcaster(_stateMachineMock.Object, _transcriberMock.Object, _outputChannelMock.Object, null!));

    public void Dispose()
    {
        _cts.Cancel();
        _sut.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        _sut.Dispose();
        _cts.Dispose();
    }
}
