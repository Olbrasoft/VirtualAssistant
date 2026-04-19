using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.Testing.Xunit.Attributes;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Service.Workers;
using Olbrasoft.VirtualAssistant.Service.Workers.Dictation;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Workers;

public class DictationWorkerTests : IDisposable
{
    private readonly Mock<ILogger<DictationWorker>> _loggerMock;
    private readonly Mock<IDictationKeyHandler> _keyHandlerMock;
    private readonly Mock<IDictationStateMachine> _stateMachineMock;
    private readonly Mock<IDictationRecordingSession> _recordingSessionMock;
    private readonly Mock<IDictationTranscriber> _transcriberMock;
    private readonly Mock<IDictationOutputChannel> _outputChannelMock;
    private readonly Mock<IDictationCompletionPipeline> _completionPipelineMock;
    private readonly DictationOptions _options;
    private readonly DictationWorker _sut;

    // Bindings captured from _keyHandler.Start(bindings); the tests invoke this
    // to simulate key events instead of reaching into an IKeyboardMonitor mock.
    private IDictationKeyHandlerBindings? _capturedBindings;

    public DictationWorkerTests()
    {
        _loggerMock = new Mock<ILogger<DictationWorker>>();
        _keyHandlerMock = new Mock<IDictationKeyHandler>();
        _stateMachineMock = new Mock<IDictationStateMachine>();
        _recordingSessionMock = new Mock<IDictationRecordingSession>();
        _transcriberMock = new Mock<IDictationTranscriber>();
        _outputChannelMock = new Mock<IDictationOutputChannel>();
        _completionPipelineMock = new Mock<IDictationCompletionPipeline>();

        _options = new DictationOptions { KeyboardLedSettleTimeMs = 10 };

        _keyHandlerMock
            .Setup(x => x.Start(It.IsAny<IDictationKeyHandlerBindings>()))
            .Callback<IDictationKeyHandlerBindings>(b => _capturedBindings = b);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);

        _sut = new DictationWorker(
            _loggerMock.Object,
            _keyHandlerMock.Object,
            _stateMachineMock.Object,
            _recordingSessionMock.Object,
            _transcriberMock.Object,
            _outputChannelMock.Object,
            _completionPipelineMock.Object,
            Options.Create(_options));
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            null!, _keyHandlerMock.Object, _stateMachineMock.Object, _recordingSessionMock.Object,
            _transcriberMock.Object, _outputChannelMock.Object, _completionPipelineMock.Object,
            Options.Create(_options)));

    [Fact]
    public void Constructor_NullKeyHandler_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object, null!, _stateMachineMock.Object, _recordingSessionMock.Object,
            _transcriberMock.Object, _outputChannelMock.Object, _completionPipelineMock.Object,
            Options.Create(_options)));

    [Fact]
    public void Constructor_NullStateMachine_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object, _keyHandlerMock.Object, null!, _recordingSessionMock.Object,
            _transcriberMock.Object, _outputChannelMock.Object, _completionPipelineMock.Object,
            Options.Create(_options)));

    [Fact]
    public void Constructor_NullRecordingSession_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object, _keyHandlerMock.Object, _stateMachineMock.Object, null!,
            _transcriberMock.Object, _outputChannelMock.Object, _completionPipelineMock.Object,
            Options.Create(_options)));

    [Fact]
    public void Constructor_NullTranscriber_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object, _keyHandlerMock.Object, _stateMachineMock.Object, _recordingSessionMock.Object,
            null!, _outputChannelMock.Object, _completionPipelineMock.Object,
            Options.Create(_options)));

    [Fact]
    public void Constructor_NullOutputChannel_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object, _keyHandlerMock.Object, _stateMachineMock.Object, _recordingSessionMock.Object,
            _transcriberMock.Object, null!, _completionPipelineMock.Object,
            Options.Create(_options)));

    [Fact]
    public void Constructor_NullCompletionPipeline_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object, _keyHandlerMock.Object, _stateMachineMock.Object, _recordingSessionMock.Object,
            _transcriberMock.Object, _outputChannelMock.Object, null!,
            Options.Create(_options)));

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object, _keyHandlerMock.Object, _stateMachineMock.Object, _recordingSessionMock.Object,
            _transcriberMock.Object, _outputChannelMock.Object, _completionPipelineMock.Object,
            null!));

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_StartsKeyHandlerWithBindings()
    {
        using var cts = new CancellationTokenSource();

        _ = _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        _keyHandlerMock.Verify(x => x.Start(It.IsAny<IDictationKeyHandlerBindings>()), Times.Once);
        Assert.NotNull(_capturedBindings);
    }

    [Fact]
    public async Task StopAsync_StopsKeyHandler()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        await _sut.StopAsync(CancellationToken.None);

        _keyHandlerMock.Verify(x => x.Stop(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_SubscribesToRawTranscriptionReadyEvent()
    {
        using var cts = new CancellationTokenSource();

        _ = _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        _transcriberMock.VerifyAdd(x => x.RawTranscriptionReady += It.IsAny<Action<string>>(), Times.Once);
    }

    [Fact]
    public async Task StopAsync_UnsubscribesFromRawTranscriptionReadyEvent()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        await _sut.StopAsync(CancellationToken.None);

        _transcriberMock.VerifyRemove(x => x.RawTranscriptionReady -= It.IsAny<Action<string>>(), Times.Once);
    }

    #endregion

    #region Bindings Adapter Tests

    // The worker's nested KeyHandlerBindings adapter is the only place the
    // worker's internal state reaches the key handler. Exercise it via the
    // captured IDictationKeyHandlerBindings instance.

    [Fact]
    public async Task Bindings_StateMirrorsStateMachineCurrentState()
    {
        using var cts = new CancellationTokenSource();
        _ = _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Transcribing);

        Assert.NotNull(_capturedBindings);
        Assert.Equal(DictationState.Transcribing, _capturedBindings!.State);
    }

    [Fact]
    public async Task Bindings_IsEnabledReflectsSetDictationEnabled()
    {
        using var cts = new CancellationTokenSource();
        _ = _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        Assert.NotNull(_capturedBindings);
        Assert.True(_capturedBindings!.IsEnabled);

        _sut.SetDictationEnabled(false);
        Assert.False(_capturedBindings.IsEnabled);
    }

    [SkipOnCIFact]
    public async Task Bindings_StartAsync_TriggersNormalModeRecording()
    {
        using var cts = new CancellationTokenSource();
        _ = _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);
        _recordingSessionMock.Setup(x => x.StartAsync(It.IsAny<bool>())).Returns(Task.CompletedTask);

        await _capturedBindings!.StartAsync();

        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Recording), Times.Once);
        _recordingSessionMock.Verify(x => x.StartAsync(It.IsAny<bool>()), Times.Once);
    }

    [SkipOnCIFact]
    public async Task Bindings_StopAndTranscribeAsync_InvokesCompletionPipelineFullMode()
    {
        using var cts = new CancellationTokenSource();
        _ = _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        var audio = new byte[] { 1, 2, 3, 4 };
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingSessionMock.Setup(x => x.StopAsync()).ReturnsAsync(audio);

        await _capturedBindings!.StopAndTranscribeAsync();
        await Task.Delay(100);

        _completionPipelineMock.Verify(
            x => x.CompleteFullAsync(audio, It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [SkipOnCIFact]
    public async Task Bindings_CancelRecordingAsync_EmergencyStopsAndPlaysCancelCue()
    {
        using var cts = new CancellationTokenSource();
        _ = _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        await _capturedBindings!.CancelRecordingAsync();

        _recordingSessionMock.Verify(x => x.EmergencyStopAsync(), Times.Once);
        _outputChannelMock.Verify(x => x.PlayCancelCue(), Times.Once);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [SkipOnCIFact]
    public async Task Bindings_CancelTranscription_DelegatesToWorker()
    {
        using var cts = new CancellationTokenSource();
        _ = _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Transcribing);

        _capturedBindings!.CancelTranscription();

        _outputChannelMock.Verify(x => x.StopTypingFeedback(), Times.Once);
        _outputChannelMock.Verify(x => x.PlayCancelCue(), Times.Once);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    #endregion

    #region SetDictationEnabled Tests

    [Fact]
    public void SetDictationEnabled_DisablingWhileIdle_DoesNotPerformEmergencyStop()
    {
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);

        _sut.SetDictationEnabled(false);

        _recordingSessionMock.Verify(x => x.EmergencyStopAsync(), Times.Never);
    }

    [Fact]
    public async Task SetDictationEnabled_DisablingWhileRecording_PerformsEmergencyStop()
    {
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingSessionMock.Setup(x => x.EmergencyStopAsync()).Returns(Task.CompletedTask);

        _sut.SetDictationEnabled(false);
        await Task.Delay(100);

        _recordingSessionMock.Verify(x => x.EmergencyStopAsync(), Times.Once);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [Fact]
    public async Task SetDictationEnabled_DisablingWhileTranscribing_PerformsEmergencyStop()
    {
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Transcribing);
        _recordingSessionMock.Setup(x => x.EmergencyStopAsync()).Returns(Task.CompletedTask);

        _sut.SetDictationEnabled(false);
        await Task.Delay(100);

        _recordingSessionMock.Verify(x => x.EmergencyStopAsync(), Times.Once);
    }

    #endregion

    #region Transcription Workflow Tests

    [SkipOnCIFact]
    public async Task StopAndTranscribe_EmptyAudio_TransitionsIdleAndSkipsCompletionPipeline()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingSessionMock.Setup(x => x.StopAsync()).ReturnsAsync(Array.Empty<byte>());

        await _sut.StopDictationAsync();
        await Task.Delay(200);

        _completionPipelineMock.Verify(
            x => x.CompleteFullAsync(It.IsAny<byte[]>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _completionPipelineMock.Verify(
            x => x.CompleteQuickAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.AtLeastOnce);
    }

    [SkipOnCIFact]
    public async Task StopAndTranscribe_NormalMode_InvokesCompleteFullAsync()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        var audioData = new byte[] { 1, 2, 3, 4 };

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingSessionMock.Setup(x => x.StopAsync()).ReturnsAsync(audioData);

        await _sut.StopDictationAsync();
        await Task.Delay(200);

        _completionPipelineMock.Verify(
            x => x.CompleteFullAsync(audioData, It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _completionPipelineMock.Verify(
            x => x.CompleteQuickAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [SkipOnCIFact]
    public async Task StopAndTranscribe_NormalMode_TranscriptionCompletedCallbackRaisesWorkerEvent()
    {
        // The worker's public TranscriptionCompleted event must fire for any
        // text the pipeline hands back via the callback — this is how the
        // SignalR broadcast + remote UI see the text before the keystrokes
        // land.
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        var audioData = new byte[] { 1, 2, 3, 4 };
        Action<string>? capturedCallback = null;
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingSessionMock.Setup(x => x.StopAsync()).ReturnsAsync(audioData);
        _completionPipelineMock
            .Setup(x => x.CompleteFullAsync(
                It.IsAny<byte[]>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
            .Callback<byte[], Action<string>, CancellationToken>((_, cb, _) => capturedCallback = cb)
            .Returns(Task.CompletedTask);

        string? workerEventText = null;
        ((IDictationService)_sut).TranscriptionCompleted += (_, text) => workerEventText = text;

        await _sut.StopDictationAsync();
        await Task.Delay(200);

        Assert.NotNull(capturedCallback);
        capturedCallback!("final text");

        Assert.Equal("final text", workerEventText);
    }

    #endregion

    #region StopAsync Tests

    [Fact]
    public async Task StopAsync_WhileIdle_DoesNotPerformEmergencyStop()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);

        await _sut.StopAsync(CancellationToken.None);

        _recordingSessionMock.Verify(x => x.EmergencyStopAsync(), Times.Never);
    }

    [Fact]
    public async Task StopAsync_WhileRecording_StopsRecording()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingSessionMock.Setup(x => x.EmergencyStopAsync()).Returns(Task.CompletedTask);

        await _sut.StopAsync(CancellationToken.None);

        _recordingSessionMock.Verify(x => x.EmergencyStopAsync(), Times.AtLeastOnce);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.AtLeastOnce);
    }

    #endregion

    #region Quick Dictation Tests

    [Fact]
    public async Task StartQuickDictationAsync_SetsQuickModeAndStartsRecording()
    {
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);

        await _sut.StartQuickDictationAsync();

        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Recording), Times.Once);
        _recordingSessionMock.Verify(x => x.StartAsync(It.IsAny<bool>()), Times.Once);
    }

    [SkipOnCIFact]
    public async Task QuickDictation_InvokesCompleteQuickAsync()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        var audioData = new byte[] { 1, 2, 3, 4 };

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);
        await _sut.StartQuickDictationAsync();

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingSessionMock.Setup(x => x.StopAsync()).ReturnsAsync(audioData);

        await _sut.StopDictationAsync();
        await Task.Delay(200);

        _completionPipelineMock.Verify(
            x => x.CompleteQuickAsync(audioData, It.IsAny<CancellationToken>()),
            Times.Once);
        _completionPipelineMock.Verify(
            x => x.CompleteFullAsync(It.IsAny<byte[]>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    public void Dispose()
    {
        _sut?.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
    }
}
