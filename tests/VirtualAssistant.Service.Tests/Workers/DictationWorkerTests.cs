using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.Testing.Xunit.Attributes;
using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Keyboard;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Service.Workers;
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
    private readonly Mock<IKeyboardSimulationService> _keyboardSimulationMock;
    private readonly Mock<ISoundEffectPlayer> _typingSoundMock;
    private readonly Mock<ISoundEffectPlayer> _cancelSoundMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IDictationPersistenceService> _persistenceServiceMock;
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
        _keyboardSimulationMock = new Mock<IKeyboardSimulationService>();
        _typingSoundMock = new Mock<ISoundEffectPlayer>();
        _cancelSoundMock = new Mock<ISoundEffectPlayer>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _persistenceServiceMock = new Mock<IDictationPersistenceService>();

        _options = new DictationOptions { KeyboardLedSettleTimeMs = 10 };

        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(_scopeMock.Object);
        _scopeMock.Setup(x => x.ServiceProvider).Returns(_serviceProviderMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(IDictationPersistenceService)))
            .Returns(_persistenceServiceMock.Object);

        _keyboardMonitorMock.SetupAdd(x => x.KeyReleased += It.IsAny<EventHandler<KeyEventArgs>>())
            .Callback<EventHandler<KeyEventArgs>>(handler => _capturedKeyReleasedHandler = handler);

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Idle);

        _sut = new DictationWorker(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            _stateMachineMock.Object,
            _recordingCoordinatorMock.Object,
            _transcriptionServiceMock.Object,
            _keyboardSimulationMock.Object,
            _typingSoundMock.Object,
            _cancelSoundMock.Object,
            _scopeFactoryMock.Object,
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
            _keyboardSimulationMock.Object,
            _typingSoundMock.Object,
            _cancelSoundMock.Object,
            _scopeFactoryMock.Object,
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
            _keyboardSimulationMock.Object,
            _typingSoundMock.Object,
            _cancelSoundMock.Object,
            _scopeFactoryMock.Object,
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
            _keyboardSimulationMock.Object,
            _typingSoundMock.Object,
            _cancelSoundMock.Object,
            _scopeFactoryMock.Object,
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
            _keyboardSimulationMock.Object,
            _typingSoundMock.Object,
            _cancelSoundMock.Object,
            _scopeFactoryMock.Object,
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
            _keyboardSimulationMock.Object,
            _typingSoundMock.Object,
            _cancelSoundMock.Object,
            _scopeFactoryMock.Object,
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
            _keyboardSimulationMock.Object,
            _typingSoundMock.Object,
            _cancelSoundMock.Object,
            _scopeFactoryMock.Object,
            Options.Create(_options)));
    }

    [Fact]
    public void Constructor_NullKeyboardSimulation_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            _stateMachineMock.Object,
            _recordingCoordinatorMock.Object,
            _transcriptionServiceMock.Object,
            null!,
            _typingSoundMock.Object,
            _cancelSoundMock.Object,
            _scopeFactoryMock.Object,
            Options.Create(_options)));
    }

    [Fact]
    public void Constructor_NullTypingSound_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            _stateMachineMock.Object,
            _recordingCoordinatorMock.Object,
            _transcriptionServiceMock.Object,
            _keyboardSimulationMock.Object,
            null!,
            _cancelSoundMock.Object,
            _scopeFactoryMock.Object,
            Options.Create(_options)));
    }

    [Fact]
    public void Constructor_NullCancelSound_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            _stateMachineMock.Object,
            _recordingCoordinatorMock.Object,
            _transcriptionServiceMock.Object,
            _keyboardSimulationMock.Object,
            _typingSoundMock.Object,
            null!,
            _scopeFactoryMock.Object,
            Options.Create(_options)));
    }

    [Fact]
    public void Constructor_NullScopeFactory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DictationWorker(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            _stateMachineMock.Object,
            _recordingCoordinatorMock.Object,
            _transcriptionServiceMock.Object,
            _keyboardSimulationMock.Object,
            _typingSoundMock.Object,
            _cancelSoundMock.Object,
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

        _typingSoundMock.Verify(x => x.StopLoop(), Times.Once);
        _cancelSoundMock.Verify(x => x.Play(), Times.Once);
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
        _keyboardSimulationMock.Setup(x => x.TypeIntoActiveWindowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _persistenceServiceMock.Setup(x => x.SaveTranscriptionAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<LlmCorrectionResult?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(300);

        _recordingCoordinatorMock.Verify(x => x.StopRecordingAsync(It.IsAny<CancellationToken>()), Times.Once);
        _typingSoundMock.Verify(x => x.StartLoop(), Times.Once);
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

        _typingSoundMock.Verify(x => x.StopLoop(), Times.AtLeastOnce);
        _stateMachineMock.Verify(x => x.TransitionTo(DictationState.Idle), Times.AtLeastOnce);
    }

    [SkipOnCIFact]
    public async Task StopAndTranscribe_SuccessfulTranscription_TypesTextAndSaves()
    {
        using var cts = new CancellationTokenSource();
        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);

        var audioData = new byte[] { 1, 2, 3, 4 };
        var transcriptionResult = new TranscriptionResult("Hello world", 0.95f)
        {
            OriginalText = "Hello world"
        };

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingCoordinatorMock.Setup(x => x.StopRecordingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(audioData);
        _transcriptionServiceMock.Setup(x => x.TranscribeAsync(audioData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transcriptionResult);
        _keyboardSimulationMock.Setup(x => x.TypeIntoActiveWindowAsync("Hello world", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _persistenceServiceMock.Setup(x => x.SaveTranscriptionAsync(
                audioData, "Hello world", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(400);

        _keyboardSimulationMock.Verify(x => x.TypeIntoActiveWindowAsync("Hello world", It.IsAny<CancellationToken>()), Times.Once);
        _persistenceServiceMock.Verify(x => x.SaveTranscriptionAsync(
            audioData, "Hello world", null, It.IsAny<CancellationToken>()), Times.Once);
        _typingSoundMock.Verify(x => x.StopLoop(), Times.AtLeastOnce);
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
            PromptId = 42
        };

        _stateMachineMock.SetupGet(x => x.CurrentState).Returns(DictationState.Recording);
        _recordingCoordinatorMock.Setup(x => x.StopRecordingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(audioData);
        _transcriptionServiceMock.Setup(x => x.TranscribeAsync(audioData, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transcriptionResult);
        _keyboardSimulationMock.Setup(x => x.TypeIntoActiveWindowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _persistenceServiceMock.Setup(x => x.SaveTranscriptionAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<LlmCorrectionResult?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(400);

        _persistenceServiceMock.Verify(x => x.SaveTranscriptionAsync(
            audioData,
            "hello world",
            It.Is<LlmCorrectionResult?>(r =>
                r != null &&
                r.CorrectedText == "Hello world corrected" &&
                r.PromptId == 42 &&
                r.DurationMs == 150),
            It.IsAny<CancellationToken>()), Times.Once);
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
        _keyboardSimulationMock.Setup(x => x.TypeIntoActiveWindowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _persistenceServiceMock.Setup(x => x.SaveTranscriptionAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<LlmCorrectionResult?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(400);

        _typingSoundMock.Verify(x => x.StopLoop(), Times.AtLeastOnce);
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

    public void Dispose()
    {
        _sut?.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
    }
}
