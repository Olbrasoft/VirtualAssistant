using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.Testing.Xunit.Attributes;
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
    private readonly Mock<IDictationCompletionPipeline> _completionPipelineMock;
    private readonly Mock<IDictationStateBroadcaster> _stateBroadcasterMock;
    private readonly Mock<IDictationCancellationCoordinator> _cancellationCoordinatorMock;
    private readonly DictationWorker _sut;

    // Bindings captured from _keyHandler.Start(bindings); tests await
    // _bindingsReady.Task to block until ExecuteAsync wired everything up.
    // RunContinuationsAsynchronously so continuations don't run inline on the
    // worker's ExecuteAsync thread. (Copilot review on PR #1035.)
    private IDictationKeyHandlerBindings? _capturedBindings;
    private readonly TaskCompletionSource<IDictationKeyHandlerBindings> _bindingsReady =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public DictationWorkerTests()
    {
        _loggerMock = new Mock<ILogger<DictationWorker>>();
        _keyHandlerMock = new Mock<IDictationKeyHandler>();
        _stateMachineMock = new Mock<IDictationStateMachine>();
        _recordingSessionMock = new Mock<IDictationRecordingSession>();
        _completionPipelineMock = new Mock<IDictationCompletionPipeline>();
        _stateBroadcasterMock = new Mock<IDictationStateBroadcaster>();
        _cancellationCoordinatorMock = new Mock<IDictationCancellationCoordinator>();

        _keyHandlerMock
            .Setup(x => x.Start(It.IsAny<IDictationKeyHandlerBindings>()))
            .Callback<IDictationKeyHandlerBindings>(b =>
            {
                _capturedBindings = b;
                _bindingsReady.TrySetResult(b);
            });

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);

        _sut = new DictationWorker(
            _loggerMock.Object,
            _keyHandlerMock.Object,
            _stateMachineMock.Object,
            _recordingSessionMock.Object,
            _completionPipelineMock.Object,
            _stateBroadcasterMock.Object,
            _cancellationCoordinatorMock.Object);
    }

    /// <summary>
    /// Starts the worker and blocks deterministically until the key handler's
    /// Start(bindings) callback fires via the TaskCompletionSource. Replaces
    /// flaky Task.Delay(50) waits. (Copilot reviews on PR #1034 + #1035.)
    /// </summary>
    private async Task<IDictationKeyHandlerBindings> StartAndAwaitBindingsAsync(CancellationToken stoppingToken = default)
    {
        _ = _sut.StartAsync(stoppingToken);
        return await _bindingsReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            null!, _keyHandlerMock.Object, _stateMachineMock.Object, _recordingSessionMock.Object,
            _completionPipelineMock.Object, _stateBroadcasterMock.Object, _cancellationCoordinatorMock.Object));

    [Fact]
    public void Constructor_NullKeyHandler_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object, null!, _stateMachineMock.Object, _recordingSessionMock.Object,
            _completionPipelineMock.Object, _stateBroadcasterMock.Object, _cancellationCoordinatorMock.Object));

    [Fact]
    public void Constructor_NullStateMachine_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object, _keyHandlerMock.Object, null!, _recordingSessionMock.Object,
            _completionPipelineMock.Object, _stateBroadcasterMock.Object, _cancellationCoordinatorMock.Object));

    [Fact]
    public void Constructor_NullRecordingSession_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object, _keyHandlerMock.Object, _stateMachineMock.Object, null!,
            _completionPipelineMock.Object, _stateBroadcasterMock.Object, _cancellationCoordinatorMock.Object));

    [Fact]
    public void Constructor_NullCompletionPipeline_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object, _keyHandlerMock.Object, _stateMachineMock.Object, _recordingSessionMock.Object,
            null!, _stateBroadcasterMock.Object, _cancellationCoordinatorMock.Object));

    [Fact]
    public void Constructor_NullStateBroadcaster_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object, _keyHandlerMock.Object, _stateMachineMock.Object, _recordingSessionMock.Object,
            _completionPipelineMock.Object, null!, _cancellationCoordinatorMock.Object));

    [Fact]
    public void Constructor_NullCancellationCoordinator_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object, _keyHandlerMock.Object, _stateMachineMock.Object, _recordingSessionMock.Object,
            _completionPipelineMock.Object, _stateBroadcasterMock.Object, null!));

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_StartsKeyHandlerWithBindings()
    {
        using var cts = new CancellationTokenSource();

        var bindings = await StartAndAwaitBindingsAsync(cts.Token);

        _keyHandlerMock.Verify(x => x.Start(It.IsAny<IDictationKeyHandlerBindings>()), Times.Once);
        Assert.NotNull(bindings);
    }

    [Fact]
    public async Task StopAsync_StopsKeyHandler()
    {
        using var cts = new CancellationTokenSource();
        await StartAndAwaitBindingsAsync(cts.Token);

        await _sut.StopAsync(CancellationToken.None);

        _keyHandlerMock.Verify(x => x.Stop(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_StartsStateBroadcaster()
    {
        using var cts = new CancellationTokenSource();

        await StartAndAwaitBindingsAsync(cts.Token);

        _stateBroadcasterMock.Verify(
            x => x.Start(It.IsAny<Action<EventHandler<string>>>(), It.IsAny<Action<EventHandler<string>>>()),
            Times.Once);
    }

    [Fact]
    public async Task StopAsync_StopsStateBroadcaster()
    {
        using var cts = new CancellationTokenSource();
        await StartAndAwaitBindingsAsync(cts.Token);

        await _sut.StopAsync(CancellationToken.None);

        _stateBroadcasterMock.Verify(x => x.Stop(), Times.Once);
    }

    #endregion

    #region Bindings Adapter Tests

    [Fact]
    public async Task Bindings_StateMirrorsStateMachineCurrentState()
    {
        using var cts = new CancellationTokenSource();
        var bindings = await StartAndAwaitBindingsAsync(cts.Token);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Transcribing);

        Assert.Equal(DictationState.Transcribing, bindings.State);
    }

    [Fact]
    public async Task Bindings_IsEnabledReflectsSetDictationEnabled()
    {
        using var cts = new CancellationTokenSource();
        var bindings = await StartAndAwaitBindingsAsync(cts.Token);

        Assert.True(bindings.IsEnabled);

        _sut.SetDictationEnabled(false);
        Assert.False(bindings.IsEnabled);
    }

    [SkipOnCIFact]
    public async Task Bindings_StartAsync_TriggersNormalModeRecording()
    {
        using var cts = new CancellationTokenSource();
        var bindings = await StartAndAwaitBindingsAsync(cts.Token);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);
        _recordingSessionMock.Setup(x => x.StartAsync(It.IsAny<bool>())).Returns(Task.CompletedTask);

        await bindings.StartAsync();

        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Recording), Times.Once);
        _recordingSessionMock.Verify(x => x.StartAsync(It.IsAny<bool>()), Times.Once);
    }

    [SkipOnCIFact]
    public async Task Bindings_StopAndTranscribeAsync_InvokesCompletionPipelineFullMode()
    {
        using var cts = new CancellationTokenSource();
        var bindings = await StartAndAwaitBindingsAsync(cts.Token);

        var audio = new byte[] { 1, 2, 3, 4 };
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingSessionMock.Setup(x => x.StopAsync()).ReturnsAsync(audio);

        await bindings.StopAndTranscribeAsync();
        await Task.Delay(100);

        _completionPipelineMock.Verify(
            x => x.CompleteFullAsync(audio, It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Bindings_CancelRecordingAsync_DelegatesToCoordinator()
    {
        using var cts = new CancellationTokenSource();
        var bindings = await StartAndAwaitBindingsAsync(cts.Token);

        await bindings.CancelRecordingAsync();

        _cancellationCoordinatorMock.Verify(x => x.CancelRecordingAsync(), Times.Once);
    }

    [Fact]
    public async Task Bindings_CancelTranscription_DelegatesToCoordinator()
    {
        using var cts = new CancellationTokenSource();
        var bindings = await StartAndAwaitBindingsAsync(cts.Token);

        bindings.CancelTranscription();

        _cancellationCoordinatorMock.Verify(x => x.CancelTranscription(), Times.Once);
    }

    #endregion

    #region SetDictationEnabled Tests

    [Fact]
    public void SetDictationEnabled_DisablingWhileIdle_DoesNotCallCoordinator()
    {
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);

        _sut.SetDictationEnabled(false);

        _cancellationCoordinatorMock.Verify(x => x.EmergencyStopAsync(), Times.Never);
    }

    [Fact]
    public async Task SetDictationEnabled_DisablingWhileRecording_TriggersCoordinatorEmergencyStop()
    {
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _cancellationCoordinatorMock.Setup(x => x.EmergencyStopAsync()).Returns(Task.CompletedTask);

        _sut.SetDictationEnabled(false);
        await Task.Delay(100);

        _cancellationCoordinatorMock.Verify(x => x.EmergencyStopAsync(), Times.Once);
    }

    [Fact]
    public async Task SetDictationEnabled_DisablingWhileTranscribing_TriggersCoordinatorEmergencyStop()
    {
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Transcribing);
        _cancellationCoordinatorMock.Setup(x => x.EmergencyStopAsync()).Returns(Task.CompletedTask);

        _sut.SetDictationEnabled(false);
        await Task.Delay(100);

        _cancellationCoordinatorMock.Verify(x => x.EmergencyStopAsync(), Times.Once);
    }

    #endregion

    #region Transcription Workflow Tests

    [SkipOnCIFact]
    public async Task StopAndTranscribe_EmptyAudio_TransitionsIdleAndSkipsCompletionPipeline()
    {
        using var cts = new CancellationTokenSource();
        await StartAndAwaitBindingsAsync(cts.Token);

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
    public async Task StopAndTranscribe_NormalMode_BeginsTranscriptionAndInvokesCompleteFullAsync()
    {
        using var cts = new CancellationTokenSource();
        await StartAndAwaitBindingsAsync(cts.Token);

        var audioData = new byte[] { 1, 2, 3, 4 };
        var token = new CancellationToken();
        _cancellationCoordinatorMock.Setup(x => x.BeginTranscription()).Returns(token);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingSessionMock.Setup(x => x.StopAsync()).ReturnsAsync(audioData);

        await _sut.StopDictationAsync();
        await Task.Delay(200);

        _cancellationCoordinatorMock.Verify(x => x.BeginTranscription(), Times.Once);
        _completionPipelineMock.Verify(
            x => x.CompleteFullAsync(audioData, It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _cancellationCoordinatorMock.Verify(x => x.EndTranscription(), Times.Once);
    }

    [SkipOnCIFact]
    public async Task StopAndTranscribe_NormalMode_TranscriptionCompletedCallbackRaisesWorkerEvent()
    {
        using var cts = new CancellationTokenSource();
        await StartAndAwaitBindingsAsync(cts.Token);

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
    public async Task StopAsync_WhileIdle_DoesNotTriggerCoordinatorShutdown()
    {
        using var cts = new CancellationTokenSource();
        await StartAndAwaitBindingsAsync(cts.Token);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);

        await _sut.StopAsync(CancellationToken.None);

        _cancellationCoordinatorMock.Verify(x => x.ShutdownAsync(), Times.Never);
    }

    [Fact]
    public async Task StopAsync_WhileRecording_TriggersCoordinatorShutdown()
    {
        using var cts = new CancellationTokenSource();
        await StartAndAwaitBindingsAsync(cts.Token);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _cancellationCoordinatorMock.Setup(x => x.ShutdownAsync()).Returns(Task.CompletedTask);

        await _sut.StopAsync(CancellationToken.None);

        _cancellationCoordinatorMock.Verify(x => x.ShutdownAsync(), Times.AtLeastOnce);
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
        await StartAndAwaitBindingsAsync(cts.Token);

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

    [SkipOnCIFact]
    public async Task KeyboardDictation_AfterQuickMode_ResetsToNormalMode()
    {
        // After a quick-mode session is canceled, the next keyboard-triggered
        // dictation (routed through bindings.StartAsync) must reset the mode
        // flag and run CompleteFullAsync, not CompleteQuickAsync.
        using var cts = new CancellationTokenSource();
        var bindings = await StartAndAwaitBindingsAsync(cts.Token);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);
        await _sut.StartQuickDictationAsync();

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        ((IDictationService)_sut).CancelTranscription();

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);
        _recordingSessionMock.Setup(x => x.StartAsync(It.IsAny<bool>())).Returns(Task.CompletedTask);
        await bindings.StartAsync();

        var audioData = new byte[] { 1, 2, 3 };
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingSessionMock.Setup(x => x.StopAsync()).ReturnsAsync(audioData);
        await bindings.StopAndTranscribeAsync();
        await Task.Delay(200);

        _completionPipelineMock.Verify(
            x => x.CompleteFullAsync(audioData, It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        _completionPipelineMock.Verify(
            x => x.CompleteQuickAsync(audioData, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    public void Dispose()
    {
        _sut?.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
    }
}
