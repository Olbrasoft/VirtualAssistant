using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.Testing.Xunit.Attributes;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;
using Olbrasoft.VirtualAssistant.Service.Hubs;
using Olbrasoft.VirtualAssistant.Service.Workers;
using Olbrasoft.VirtualAssistant.Service.Workers.Dictation;
using Olbrasoft.VirtualAssistant.Service.Workers.Streaming;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Workers;

public class DictationWorkerTests : IDisposable
{
    private readonly Mock<ILogger<DictationWorker>> _loggerMock;
    private readonly Mock<IKeyboardMonitor> _keyboardMonitorMock;
    private readonly Mock<IDictationStateMachine> _stateMachineMock;
    private readonly Mock<IDictationRecordingSession> _recordingSessionMock;
    private readonly Mock<IDictationTranscriber> _transcriberMock;
    private readonly Mock<IDictationOutputChannel> _outputChannelMock;
    private readonly Mock<IDictationCompletionPipeline> _completionPipelineMock;
    private readonly DictationOptions _options;
    private readonly DictationWorker _sut;

    private EventHandler<KeyEventArgs>? _capturedKeyReleasedHandler;

    public DictationWorkerTests()
    {
        _loggerMock = new Mock<ILogger<DictationWorker>>();
        _keyboardMonitorMock = new Mock<IKeyboardMonitor>();
        _stateMachineMock = new Mock<IDictationStateMachine>();
        _recordingSessionMock = new Mock<IDictationRecordingSession>();
        _transcriberMock = new Mock<IDictationTranscriber>();
        _outputChannelMock = new Mock<IDictationOutputChannel>();
        _completionPipelineMock = new Mock<IDictationCompletionPipeline>();

        _options = new DictationOptions { KeyboardLedSettleTimeMs = 10 };

        _keyboardMonitorMock.SetupAdd(x => x.KeyReleased += It.IsAny<EventHandler<KeyEventArgs>>())
            .Callback<EventHandler<KeyEventArgs>>(handler => _capturedKeyReleasedHandler = handler);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);

        _sut = new DictationWorker(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            _stateMachineMock.Object,
            _recordingSessionMock.Object,
            _transcriberMock.Object,
            _outputChannelMock.Object,
            _completionPipelineMock.Object,
            Options.Create(_options));
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            null!,
            _keyboardMonitorMock.Object,
            _stateMachineMock.Object,
            _recordingSessionMock.Object,
            _transcriberMock.Object,
            _outputChannelMock.Object,
            _completionPipelineMock.Object,
            Options.Create(_options)));
    }

    [Fact]
    public void Constructor_NullKeyboardMonitor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object,
            null!,
            _stateMachineMock.Object,
            _recordingSessionMock.Object,
            _transcriberMock.Object,
            _outputChannelMock.Object,
            _completionPipelineMock.Object,
            Options.Create(_options)));
    }

    [Fact]
    public void Constructor_NullStateMachine_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            null!,
            _recordingSessionMock.Object,
            _transcriberMock.Object,
            _outputChannelMock.Object,
            _completionPipelineMock.Object,
            Options.Create(_options)));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            _stateMachineMock.Object,
            _recordingSessionMock.Object,
            _transcriberMock.Object,
            _outputChannelMock.Object,
            _completionPipelineMock.Object,
            null!));
    }

    [Fact]
    public void Constructor_NullRecordingSession_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            _stateMachineMock.Object,
            null!,
            _transcriberMock.Object,
            _outputChannelMock.Object,
            _completionPipelineMock.Object,
            Options.Create(_options)));
    }

    [Fact]
    public void Constructor_NullTranscriber_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            _stateMachineMock.Object,
            _recordingSessionMock.Object,
            null!,
            _outputChannelMock.Object,
            _completionPipelineMock.Object,
            Options.Create(_options)));
    }

    [Fact]
    public void Constructor_NullOutputChannel_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            _stateMachineMock.Object,
            _recordingSessionMock.Object,
            _transcriberMock.Object,
            null!,
            _completionPipelineMock.Object,
            Options.Create(_options)));
    }

    [Fact]
    public void Constructor_NullCompletionPipeline_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            _stateMachineMock.Object,
            _recordingSessionMock.Object,
            _transcriberMock.Object,
            _outputChannelMock.Object,
            null!,
            Options.Create(_options)));
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_SubscribesToKeyReleasedEvent()
    {
        using var cts = new CancellationTokenSource();

        _ = _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        _keyboardMonitorMock.VerifyAdd(x => x.KeyReleased += It.IsAny<EventHandler<KeyEventArgs>>(), Times.Once);
    }

    [Fact]
    public async Task StopAsync_UnsubscribesFromKeyReleasedEvent()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        await _sut.StopAsync(CancellationToken.None);

        _keyboardMonitorMock.VerifyRemove(x => x.KeyReleased -= It.IsAny<EventHandler<KeyEventArgs>>(), Times.Once);
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
        _recordingSessionMock.Setup(x => x.EmergencyStopAsync())
            .Returns(Task.CompletedTask);

        _sut.SetDictationEnabled(false);
        await Task.Delay(100);

        _recordingSessionMock.Verify(x => x.EmergencyStopAsync(), Times.Once);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [Fact]
    public async Task SetDictationEnabled_DisablingWhileTranscribing_PerformsEmergencyStop()
    {
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Transcribing);
        _recordingSessionMock.Setup(x => x.EmergencyStopAsync())
            .Returns(Task.CompletedTask);

        _sut.SetDictationEnabled(false);
        await Task.Delay(100);

        _recordingSessionMock.Verify(x => x.EmergencyStopAsync(), Times.Once);
    }

    #endregion

    #region Key Event Handling Tests

    [SkipOnCIFact]
    public async Task KeyReleased_ScrollLockWhileIdle_StartsRecording()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);
        _recordingSessionMock.Setup(x => x.StartAsync(It.IsAny<bool>()))
            .Returns(Task.CompletedTask);

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(150);

        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Recording), Times.Once);
        _recordingSessionMock.Verify(x => x.StartAsync(It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task KeyReleased_NonScrollLockKey_DoesNotStartRecording()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.Escape, IsPressed = false });
        await Task.Delay(100);

        _stateMachineMock.Verify(x => x.TransitionTo(It.IsAny<DictationState>()), Times.Never);
        _recordingSessionMock.Verify(x => x.StartAsync(It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task KeyReleased_WhenDictationDisabled_IgnoresEvent()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        _sut.SetDictationEnabled(false);

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(100);

        _stateMachineMock.Verify(x => x.TransitionTo(It.IsAny<DictationState>()), Times.Never);
    }

    [SkipOnCIFact]
    public async Task KeyReleased_PauseWhileTranscribing_CancelsTranscription()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Transcribing);

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.Pause, IsPressed = false });
        await Task.Delay(100);

        _outputChannelMock.Verify(x => x.StopTypingFeedback(), Times.Once);
        _outputChannelMock.Verify(x => x.PlayCancelCue(), Times.Once);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [SkipOnCIFact]
    public async Task KeyReleased_PauseWhileRecording_CancelsRecording()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.Pause, IsPressed = false });
        await Task.Delay(100);

        _recordingSessionMock.Verify(x => x.EmergencyStopAsync(), Times.Once);
        _outputChannelMock.Verify(x => x.PlayCancelCue(), Times.Once);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [SkipOnCIFact]
    public async Task KeyReleased_ScrollLockWhileRecording_StopsRecordingAndInvokesPipeline()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        var audioData = new byte[] { 1, 2, 3, 4 };

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingSessionMock.Setup(x => x.StopAsync())
            .ReturnsAsync(audioData);

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(300);

        _recordingSessionMock.Verify(x => x.StopAsync(), Times.Once);
        _completionPipelineMock.Verify(
            x => x.CompleteFullAsync(audioData, It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task KeyReleased_ScrollLockWhileTranscribing_IsIgnored()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Transcribing);

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(100);

        // ScrollLock during transcription should be ignored (use Pause to cancel)
        _stateMachineMock.Verify(x => x.TransitionTo(It.IsAny<DictationState>()), Times.Never);
        _recordingSessionMock.Verify(x => x.StartAsync(It.IsAny<bool>()), Times.Never);
        _recordingSessionMock.Verify(x => x.StopAsync(), Times.Never);
    }

    #endregion

    #region Transcription Workflow Tests

    [SkipOnCIFact]
    public async Task StopAndTranscribe_EmptyAudio_SkipsCompletionPipeline()
    {
        // ValidateAndPrepareAudioAsync returns null on empty buffer; the
        // pipeline must never be invoked. Transcription workflow internals
        // (save + type + state transitions) are covered in
        // DictationCompletionPipelineTests.
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingSessionMock.Setup(x => x.StopAsync())
            .ReturnsAsync(Array.Empty<byte>());

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(200);

        _completionPipelineMock.Verify(
            x => x.CompleteFullAsync(It.IsAny<byte[]>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _completionPipelineMock.Verify(
            x => x.CompleteQuickAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [SkipOnCIFact]
    public async Task StopAndTranscribe_NormalMode_InvokesCompleteFullAsync()
    {
        // Worker's only job in normal dictation is to stop recording, hand
        // the audio to the completion pipeline's full-mode method, and wire
        // the TranscriptionCompleted callback. The actual pipeline behavior
        // (save, type, state transitions) is covered in
        // DictationCompletionPipelineTests.
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        var audioData = new byte[] { 1, 2, 3, 4 };

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingSessionMock.Setup(x => x.StopAsync())
            .ReturnsAsync(audioData);

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(200);

        _completionPipelineMock.Verify(
            x => x.CompleteFullAsync(
                audioData,
                It.IsAny<Action<string>>(),
                It.IsAny<CancellationToken>()),
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
        _recordingSessionMock.Setup(x => x.StopAsync())
            .ReturnsAsync(audioData);
        _completionPipelineMock
            .Setup(x => x.CompleteFullAsync(
                It.IsAny<byte[]>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
            .Callback<byte[], Action<string>, CancellationToken>((_, cb, _) => capturedCallback = cb)
            .Returns(Task.CompletedTask);

        string? workerEventText = null;
        ((IDictationService)_sut).TranscriptionCompleted += (_, text) => workerEventText = text;

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
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
        _recordingSessionMock.Setup(x => x.EmergencyStopAsync())
            .Returns(Task.CompletedTask);

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
        // Worker's quick-mode contract: StartQuickDictationAsync flips the
        // flag, and on stop the worker hands audio to CompleteQuickAsync.
        // Fast-paste / civility-trim / broadcast internals are covered in
        // DictationCompletionPipelineTests.
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        var audioData = new byte[] { 1, 2, 3, 4 };

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);
        await _sut.StartQuickDictationAsync();

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingSessionMock.Setup(x => x.StopAsync())
            .ReturnsAsync(audioData);

        await _sut.StopDictationAsync();
        await Task.Delay(200);

        _completionPipelineMock.Verify(
            x => x.CompleteQuickAsync(audioData, It.IsAny<CancellationToken>()),
            Times.Once);
        _completionPipelineMock.Verify(
            x => x.CompleteFullAsync(It.IsAny<byte[]>(), It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [SkipOnCIFact]
    public async Task KeyboardDictation_AfterQuickMode_ResetsToNormalMode()
    {
        // After canceling a quick-mode session, the next ScrollLock-triggered
        // dictation must go through the full-mode pipeline, not the quick one.
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);
        await _sut.StartQuickDictationAsync();

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        ((IDictationService)_sut).CancelTranscription();
        await Task.Delay(50);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);
        _recordingSessionMock.Setup(x => x.StartAsync(It.IsAny<bool>()))
            .Returns(Task.CompletedTask);

        var audioData = new byte[] { 1, 2, 3 };

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(100);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingSessionMock.Setup(x => x.StopAsync())
            .ReturnsAsync(audioData);

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(200);

        _completionPipelineMock.Verify(
            x => x.CompleteFullAsync(audioData, It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    #endregion

    public void Dispose()
    {
        _sut?.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
    }
}
