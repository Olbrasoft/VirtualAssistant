using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.Testing.Xunit.Attributes;
using Olbrasoft.VirtualAssistant.Core.Audio;
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
    private readonly Mock<IAudioRecordingCoordinator> _recordingCoordinatorMock;
    private readonly Mock<ITranscriptionService> _transcriptionServiceMock;
    private readonly Mock<IDictationOutputChannel> _outputChannelMock;
    private readonly Mock<IDictationTranscriptionPersister> _persisterMock;
    private readonly Mock<IClaudeCodeCivilityTrimmer> _civilityTrimmerMock;
    private readonly Mock<IStreamingChunkAssembler> _streamingAssemblerMock;
    private readonly DictationOptions _options;
    private readonly DictationWorker _sut;

    private EventHandler<KeyEventArgs>? _capturedKeyReleasedHandler;

    public DictationWorkerTests()
    {
        _loggerMock = new Mock<ILogger<DictationWorker>>();
        _keyboardMonitorMock = new Mock<IKeyboardMonitor>();
        _stateMachineMock = new Mock<IDictationStateMachine>();
        _recordingCoordinatorMock = new Mock<IAudioRecordingCoordinator>();
        _transcriptionServiceMock = new Mock<ITranscriptionService>();
        _outputChannelMock = new Mock<IDictationOutputChannel>();
        _persisterMock = new Mock<IDictationTranscriptionPersister>();
        _civilityTrimmerMock = new Mock<IClaudeCodeCivilityTrimmer>();
        _streamingAssemblerMock = new Mock<IStreamingChunkAssembler>();

        _options = new DictationOptions { KeyboardLedSettleTimeMs = 10 };

        // The civility trimmer is always on the quick-dictation path; without
        // an explicit passthrough setup, the Mock default returns null/empty
        // which trips the "reduced to empty" short-circuit and skips paste.
        _civilityTrimmerMock
            .Setup(x => x.TrimIfClaudeCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((text, _) => Task.FromResult(text));

        _keyboardMonitorMock.SetupAdd(x => x.KeyReleased += It.IsAny<EventHandler<KeyEventArgs>>())
            .Callback<EventHandler<KeyEventArgs>>(handler => _capturedKeyReleasedHandler = handler);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);

        _sut = new DictationWorker(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            _stateMachineMock.Object,
            _recordingCoordinatorMock.Object,
            _transcriptionServiceMock.Object,
            _outputChannelMock.Object,
            _persisterMock.Object,
            _civilityTrimmerMock.Object,
            _streamingAssemblerMock.Object,
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
            _recordingCoordinatorMock.Object,
            _transcriptionServiceMock.Object,
            _outputChannelMock.Object,
            _persisterMock.Object,
            _civilityTrimmerMock.Object,
            _streamingAssemblerMock.Object,
            Options.Create(_options)));
    }

    [Fact]
    public void Constructor_NullKeyboardMonitor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object,
            null!,
            _stateMachineMock.Object,
            _recordingCoordinatorMock.Object,
            _transcriptionServiceMock.Object,
            _outputChannelMock.Object,
            _persisterMock.Object,
            _civilityTrimmerMock.Object,
            _streamingAssemblerMock.Object,
            Options.Create(_options)));
    }

    [Fact]
    public void Constructor_NullStateMachine_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            null!,
            _recordingCoordinatorMock.Object,
            _transcriptionServiceMock.Object,
            _outputChannelMock.Object,
            _persisterMock.Object,
            _civilityTrimmerMock.Object,
            _streamingAssemblerMock.Object,
            Options.Create(_options)));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            _stateMachineMock.Object,
            _recordingCoordinatorMock.Object,
            _transcriptionServiceMock.Object,
            _outputChannelMock.Object,
            _persisterMock.Object,
            _civilityTrimmerMock.Object,
            _streamingAssemblerMock.Object,
            null!));
    }

    [Fact]
    public void Constructor_NullRecordingCoordinator_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            _stateMachineMock.Object,
            null!,
            _transcriptionServiceMock.Object,
            _outputChannelMock.Object,
            _persisterMock.Object,
            _civilityTrimmerMock.Object,
            _streamingAssemblerMock.Object,
            Options.Create(_options)));
    }

    [Fact]
    public void Constructor_NullTranscriptionService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            _stateMachineMock.Object,
            _recordingCoordinatorMock.Object,
            null!,
            _outputChannelMock.Object,
            _persisterMock.Object,
            _civilityTrimmerMock.Object,
            _streamingAssemblerMock.Object,
            Options.Create(_options)));
    }

    [Fact]
    public void Constructor_NullOutputChannel_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            _stateMachineMock.Object,
            _recordingCoordinatorMock.Object,
            _transcriptionServiceMock.Object,
            null!,
            _persisterMock.Object,
            _civilityTrimmerMock.Object,
            _streamingAssemblerMock.Object,
            Options.Create(_options)));
    }

    [Fact]
    public void Constructor_NullPersister_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            _stateMachineMock.Object,
            _recordingCoordinatorMock.Object,
            _transcriptionServiceMock.Object,
            _outputChannelMock.Object,
            null!,
            _civilityTrimmerMock.Object,
            _streamingAssemblerMock.Object,
            Options.Create(_options)));
    }

    [Fact]
    public void Constructor_NullCivilityTrimmer_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            _stateMachineMock.Object,
            _recordingCoordinatorMock.Object,
            _transcriptionServiceMock.Object,
            _outputChannelMock.Object,
            _persisterMock.Object,
            null!,
            _streamingAssemblerMock.Object,
            Options.Create(_options)));
    }

    [Fact]
    public void Constructor_NullStreamingAssembler_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            _stateMachineMock.Object,
            _recordingCoordinatorMock.Object,
            _transcriptionServiceMock.Object,
            _outputChannelMock.Object,
            _persisterMock.Object,
            _civilityTrimmerMock.Object,
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

        _transcriptionServiceMock.VerifyAdd(x => x.RawTranscriptionReady += It.IsAny<Action<string>>(), Times.Once);
    }

    [Fact]
    public async Task StopAsync_UnsubscribesFromRawTranscriptionReadyEvent()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        await _sut.StopAsync(CancellationToken.None);

        _transcriptionServiceMock.VerifyRemove(x => x.RawTranscriptionReady -= It.IsAny<Action<string>>(), Times.Once);
    }

    #endregion

    #region SetDictationEnabled Tests

    [Fact]
    public void SetDictationEnabled_DisablingWhileIdle_DoesNotPerformEmergencyStop()
    {
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);

        _sut.SetDictationEnabled(false);

        _recordingCoordinatorMock.Verify(x => x.EmergencyStopAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetDictationEnabled_DisablingWhileRecording_PerformsEmergencyStop()
    {
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingCoordinatorMock.Setup(x => x.EmergencyStopAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut.SetDictationEnabled(false);
        await Task.Delay(100);

        _recordingCoordinatorMock.Verify(x => x.EmergencyStopAsync(It.IsAny<CancellationToken>()), Times.Once);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [Fact]
    public async Task SetDictationEnabled_DisablingWhileTranscribing_PerformsEmergencyStop()
    {
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Transcribing);
        _recordingCoordinatorMock.Setup(x => x.EmergencyStopAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut.SetDictationEnabled(false);
        await Task.Delay(100);

        _recordingCoordinatorMock.Verify(x => x.EmergencyStopAsync(It.IsAny<CancellationToken>()), Times.Once);
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
        _recordingCoordinatorMock.Setup(x => x.StartRecordingAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(150);

        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Recording), Times.Once);
        _recordingCoordinatorMock.Verify(x => x.StartRecordingAsync(It.IsAny<CancellationToken>()), Times.Once);
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
        _recordingCoordinatorMock.Verify(x => x.StartRecordingAsync(It.IsAny<CancellationToken>()), Times.Never);
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

        _recordingCoordinatorMock.Verify(x => x.EmergencyStopAsync(It.IsAny<CancellationToken>()), Times.Once);
        _outputChannelMock.Verify(x => x.PlayCancelCue(), Times.Once);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.Once);
    }

    [SkipOnCIFact]
    public async Task KeyReleased_ScrollLockWhileRecording_StopsAndTranscribes()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        var audioData = new byte[] { 1, 2, 3, 4 };
        var transcriptionResult = new TranscriptionResult("Test transcription", 0.95f)
        {
            OriginalText = "Test transcription"
        };

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingCoordinatorMock.Setup(x => x.StopRecordingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(audioData);
        _transcriptionServiceMock.Setup(x => x.TranscribeAsync(audioData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transcriptionResult);
        _outputChannelMock.Setup(x => x.TypeIntoActiveWindowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(300);

        _recordingCoordinatorMock.Verify(x => x.StopRecordingAsync(It.IsAny<CancellationToken>()), Times.Once);
        _outputChannelMock.Verify(x => x.StartTypingFeedback(), Times.Once);
        _transcriptionServiceMock.Verify(x => x.TranscribeAsync(audioData, It.IsAny<CancellationToken>()), Times.Once);
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
        _recordingCoordinatorMock.Verify(x => x.StartRecordingAsync(It.IsAny<CancellationToken>()), Times.Never);
        _recordingCoordinatorMock.Verify(x => x.StopRecordingAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Transcription Workflow Tests

    [SkipOnCIFact]
    public async Task StopAndTranscribe_EmptyAudio_TransitionsToIdleWithoutTranscription()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingCoordinatorMock.Setup(x => x.StopRecordingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<byte>());

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(200);

        _transcriptionServiceMock.Verify(x => x.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.AtLeastOnce);
    }

    [SkipOnCIFact]
    public async Task StopAndTranscribe_TranscriptionFails_TransitionsToIdle()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        var audioData = new byte[] { 1, 2, 3, 4 };
        var failedResult = new TranscriptionResult("Error") { OriginalText = null };

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingCoordinatorMock.Setup(x => x.StopRecordingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(audioData);
        _transcriptionServiceMock.Setup(x => x.TranscribeAsync(audioData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedResult);

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(300);

        _outputChannelMock.Verify(x => x.StopTypingFeedback(), Times.AtLeastOnce);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.AtLeastOnce);
    }

    [SkipOnCIFact]
    public async Task StopAndTranscribe_SuccessfulTranscription_TypesTextAndSaves()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        var audioData = new byte[] { 1, 2, 3, 4 };
        const int expectedProviderId = 14; // Google STT
        var transcriptionResult = new TranscriptionResult("Hello world", 0.95f)
        {
            OriginalText = "Hello world",
            SttProviderId = expectedProviderId
        };

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingCoordinatorMock.Setup(x => x.StopRecordingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(audioData);
        _transcriptionServiceMock.Setup(x => x.TranscribeAsync(audioData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transcriptionResult);
        _outputChannelMock.Setup(x => x.TypeIntoActiveWindowAsync("Hello world", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(400);

        _outputChannelMock.Verify(x => x.TypeIntoActiveWindowAsync("Hello world", It.IsAny<CancellationToken>()), Times.Once);
        // The LlmCorrectionResult building + STT-provider fallback live in the
        // persister now; the worker's contract is just "forward the raw
        // TranscriptionResult". DictationTranscriptionPersisterTests cover the
        // details.
        _persisterMock.Verify(x => x.SaveAsync(
            audioData, It.Is<TranscriptionResult>(r => r.Text == "Hello world" && r.SttProviderId == expectedProviderId), It.IsAny<CancellationToken>()),
            Times.Once);
        _outputChannelMock.Verify(x => x.StopTypingFeedback(), Times.AtLeastOnce);
    }

    [SkipOnCIFact]
    public async Task StopAndTranscribe_WithLlmCorrection_SavesCorrectedTextWithMetadata()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        var audioData = new byte[] { 1, 2, 3, 4 };
        var transcriptionResult = new TranscriptionResult("Hello world corrected", 0.95f)
        {
            OriginalText = "hello world",
            LlmDurationMs = 150,
            PromptId = 42,
            ModelId = 1
        };

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingCoordinatorMock.Setup(x => x.StopRecordingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(audioData);
        _transcriptionServiceMock.Setup(x => x.TranscribeAsync(audioData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transcriptionResult);
        _outputChannelMock.Setup(x => x.TypeIntoActiveWindowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(400);

        // Worker just forwards the TranscriptionResult (incl. LLM-correction
        // metadata) to the persister; the LlmCorrectionResult construction is
        // pinned in DictationTranscriptionPersisterTests.
        _persisterMock.Verify(x => x.SaveAsync(
            audioData,
            It.Is<TranscriptionResult>(r =>
                r.Text == "Hello world corrected" &&
                r.OriginalText == "hello world" &&
                r.PromptId == 42 &&
                r.ModelId == 1 &&
                r.LlmDurationMs == 150),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [SkipOnCIFact]
    public async Task StopAndTranscribe_KeyboardSimulationFails_TransitionsToIdle()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        var audioData = new byte[] { 1, 2, 3, 4 };
        var transcriptionResult = new TranscriptionResult("Test text", 0.95f)
        {
            OriginalText = "Test text"
        };

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingCoordinatorMock.Setup(x => x.StopRecordingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(audioData);
        _transcriptionServiceMock.Setup(x => x.TranscribeAsync(audioData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transcriptionResult);
        _outputChannelMock.Setup(x => x.TypeIntoActiveWindowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(400);

        _outputChannelMock.Verify(x => x.StopTypingFeedback(), Times.AtLeastOnce);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.AtLeastOnce);
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

        _recordingCoordinatorMock.Verify(x => x.EmergencyStopAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StopAsync_WhileRecording_StopsRecording()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingCoordinatorMock.Setup(x => x.EmergencyStopAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.StopAsync(CancellationToken.None);

        _recordingCoordinatorMock.Verify(x => x.EmergencyStopAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
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
        _recordingCoordinatorMock.Verify(x => x.StartRecordingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [SkipOnCIFact]
    public async Task QuickDictation_UsesTranscribeRawAndFastPaste()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        var audioData = new byte[] { 1, 2, 3, 4 };
        var transcriptionResult = new TranscriptionResult("Quick test", 0.95f);

        // Start quick dictation
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);
        await _sut.StartQuickDictationAsync();

        // Stop and transcribe
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingCoordinatorMock.Setup(x => x.StopRecordingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(audioData);
        _transcriptionServiceMock.Setup(x => x.TranscribeRawAsync(audioData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transcriptionResult);
        _outputChannelMock.Setup(x => x.FastPasteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.StopDictationAsync();
        await Task.Delay(400);

        // Verify raw transcription used (not full pipeline)
        _transcriptionServiceMock.Verify(x => x.TranscribeRawAsync(audioData, It.IsAny<CancellationToken>()), Times.Once);
        _transcriptionServiceMock.Verify(x => x.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);

        // Verify FastPaste used (not TypeIntoActiveWindow)
        _outputChannelMock.Verify(x => x.FastPasteAsync("Quick test", It.IsAny<CancellationToken>()), Times.Once);
        _outputChannelMock.Verify(x => x.TypeIntoActiveWindowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [SkipOnCIFact]
    public async Task QuickDictation_WhenFastPasteFails_DoesNotBroadcastQuickTranscriptionCompleted()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        var audioData = new byte[] { 1, 2, 3, 4 };
        var transcriptionResult = new TranscriptionResult("Quick test", 0.95f);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);
        await _sut.StartQuickDictationAsync();

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingCoordinatorMock.Setup(x => x.StopRecordingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(audioData);
        _transcriptionServiceMock.Setup(x => x.TranscribeRawAsync(audioData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transcriptionResult);
        _outputChannelMock.Setup(x => x.FastPasteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.StopDictationAsync();
        await Task.Delay(400);

        // Should still transition to Idle on failure — AND must not emit the
        // QuickTranscriptionCompleted broadcast, since the paste didn't land
        // (remote UI would otherwise light up as if the text went through).
        // (Copilot review on PR #1025.)
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.AtLeastOnce);
        _outputChannelMock.Verify(
            x => x.BroadcastEventAsync(DictationEventType.QuickTranscriptionCompleted, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [SkipOnCIFact]
    public async Task KeyboardDictation_AfterQuickMode_ResetsToNormalMode()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        // First: start quick dictation via hub
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);
        await _sut.StartQuickDictationAsync();

        // Cancel it
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        ((IDictationService)_sut).CancelTranscription();
        await Task.Delay(50);

        // Then: keyboard dictation via ScrollLock should use normal mode
        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);
        _recordingCoordinatorMock.Setup(x => x.StartRecordingAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var audioData = new byte[] { 1, 2, 3 };
        var result = new TranscriptionResult("Normal test", 0.9f);

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(100);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingCoordinatorMock.Setup(x => x.StopRecordingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(audioData);
        _transcriptionServiceMock.Setup(x => x.TranscribeAsync(audioData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        _outputChannelMock.Setup(x => x.TypeIntoActiveWindowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(400);

        // Verify normal transcription used (not raw)
        _transcriptionServiceMock.Verify(x => x.TranscribeAsync(audioData, It.IsAny<CancellationToken>()), Times.Once);
        _transcriptionServiceMock.Verify(x => x.TranscribeRawAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    public void Dispose()
    {
        _sut?.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
    }
}
