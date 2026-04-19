using Moq;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Service.Hubs;
using Olbrasoft.VirtualAssistant.Service.Workers.Dictation;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Workers.Dictation;

/// <summary>
/// Pins the SignalR fan-out lifted out of DictationWorker in #969. The
/// broadcaster subscribes to the state machine's StateChanged, the
/// transcriber's RawTranscriptionReady, and the worker's
/// TranscriptionCompleted (via subscribe/unsubscribe hooks), and maps each
/// emission to the matching DictationEventType broadcast.
/// </summary>
public class DictationStateBroadcasterTests
{
    private readonly Mock<IDictationStateMachine> _stateMachineMock = new();
    private readonly Mock<IDictationTranscriber> _transcriberMock = new();
    private readonly Mock<IDictationOutputChannel> _outputChannelMock = new();

    // Proxy for the worker's TranscriptionCompleted event — tests register
    // add/remove hooks and then raise the event to exercise the broadcaster.
    private event EventHandler<string>? TranscriptionCompleted;

    private DictationStateBroadcaster CreateSut() =>
        new(_stateMachineMock.Object, _transcriberMock.Object, _outputChannelMock.Object);

    private void StartWith(DictationStateBroadcaster sut) =>
        sut.Start(h => TranscriptionCompleted += h, h => TranscriptionCompleted -= h);

    [Fact]
    public void Start_SubscribesToAllEvents()
    {
        var sut = CreateSut();
        StartWith(sut);

        _stateMachineMock.VerifyAdd(x => x.StateChanged += It.IsAny<EventHandler<DictationState>>(), Times.Once);
        _transcriberMock.VerifyAdd(x => x.RawTranscriptionReady += It.IsAny<Action<string>>(), Times.Once);
    }

    [Fact]
    public void Start_Twice_SubscribesOnlyOnce()
    {
        var sut = CreateSut();
        StartWith(sut);
        StartWith(sut);

        _stateMachineMock.VerifyAdd(x => x.StateChanged += It.IsAny<EventHandler<DictationState>>(), Times.Once);
    }

    [Fact]
    public void Stop_UnsubscribesFromAllEvents()
    {
        var sut = CreateSut();
        StartWith(sut);

        sut.Stop();

        _stateMachineMock.VerifyRemove(x => x.StateChanged -= It.IsAny<EventHandler<DictationState>>(), Times.Once);
        _transcriberMock.VerifyRemove(x => x.RawTranscriptionReady -= It.IsAny<Action<string>>(), Times.Once);
    }

    [Fact]
    public void Stop_WithoutStart_DoesNotThrow()
    {
        var sut = CreateSut();
        sut.Stop(); // must be idempotent for DI shutdown

        _stateMachineMock.VerifyRemove(x => x.StateChanged -= It.IsAny<EventHandler<DictationState>>(), Times.Never);
    }

    [Fact]
    public void StateChanged_Recording_BroadcastsRecordingStarted()
    {
        var sut = CreateSut();
        StartWith(sut);

        _stateMachineMock.Raise(x => x.StateChanged += null, this, DictationState.Recording);

        _outputChannelMock.Verify(
            x => x.BroadcastEventAsync(DictationEventType.RecordingStarted, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void StateChanged_Transcribing_BroadcastsTranscriptionStarted()
    {
        var sut = CreateSut();
        StartWith(sut);

        _stateMachineMock.Raise(x => x.StateChanged += null, this, DictationState.Transcribing);

        _outputChannelMock.Verify(
            x => x.BroadcastEventAsync(DictationEventType.TranscriptionStarted, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void StateChanged_Idle_BroadcastsRecordingStopped()
    {
        // Idle is the catch-all — any non-Recording, non-Transcribing state
        // transition maps to RecordingStopped so the UI clears its pending flag.
        var sut = CreateSut();
        StartWith(sut);

        _stateMachineMock.Raise(x => x.StateChanged += null, this, DictationState.Idle);

        _outputChannelMock.Verify(
            x => x.BroadcastEventAsync(DictationEventType.RecordingStopped, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void RawTranscriptionReady_BroadcastsRawTranscriptionCompleted()
    {
        var sut = CreateSut();
        StartWith(sut);

        _transcriberMock.Raise(x => x.RawTranscriptionReady += null, "raw text");

        _outputChannelMock.Verify(
            x => x.BroadcastEventAsync(DictationEventType.RawTranscriptionCompleted, "raw text", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void TranscriptionCompleted_BroadcastsTranscriptionCompleted()
    {
        var sut = CreateSut();
        StartWith(sut);

        TranscriptionCompleted?.Invoke(this, "final text");

        _outputChannelMock.Verify(
            x => x.BroadcastEventAsync(DictationEventType.TranscriptionCompleted, "final text", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Stop_UnsubscribesTranscriptionCompletedToo()
    {
        // After Stop, raising TranscriptionCompleted must not broadcast —
        // the worker may still exist but the broadcaster is done.
        var sut = CreateSut();
        StartWith(sut);
        sut.Stop();

        TranscriptionCompleted?.Invoke(this, "after stop");

        _outputChannelMock.Verify(
            x => x.BroadcastEventAsync(It.IsAny<DictationEventType>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Constructor_NullStateMachine_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DictationStateBroadcaster(null!, _transcriberMock.Object, _outputChannelMock.Object));

    [Fact]
    public void Constructor_NullTranscriber_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DictationStateBroadcaster(_stateMachineMock.Object, null!, _outputChannelMock.Object));

    [Fact]
    public void Constructor_NullOutputChannel_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DictationStateBroadcaster(_stateMachineMock.Object, _transcriberMock.Object, null!));

    [Fact]
    public void Start_NullSubscribeHook_Throws()
    {
        var sut = CreateSut();
        Assert.Throws<ArgumentNullException>(() => sut.Start(null!, _ => { }));
    }

    [Fact]
    public void Start_NullUnsubscribeHook_Throws()
    {
        var sut = CreateSut();
        Assert.Throws<ArgumentNullException>(() => sut.Start(_ => { }, null!));
    }
}
