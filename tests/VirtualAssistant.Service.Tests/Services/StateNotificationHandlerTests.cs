using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Desktop.Services;
using Olbrasoft.VirtualAssistant.Service.Infrastructure;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="StateNotificationHandler"/>.
/// Verifies state synchronization using Observer pattern.
/// </summary>
public class StateNotificationHandlerTests
{
    private readonly Mock<ILogger<StateNotificationHandler>> _loggerMock;
    private readonly Mock<IManualMuteService> _muteServiceMock;
    private readonly Mock<ISettingsService> _settingsServiceMock;
    private readonly Mock<IServiceStatusUpdater> _statusUpdaterMock;
    private readonly Mock<ITrayIconCoordinator> _iconCoordinatorMock;
    private readonly Mock<IIconAnimationService> _iconAnimationServiceMock;
    private readonly Mock<IServiceLifecycleManager> _lifecycleManagerMock;
    private readonly Mock<IDictationStateMachine> _dictationStateMachineMock;
    private readonly Mock<IRecordingNotificationService> _recordingNotificationServiceMock;
    private readonly Mock<IRecordingOverlayService> _recordingOverlayServiceMock;
    private readonly Mock<ISoundEffectPlayer> _recordingStartSoundPlayerMock;

    public StateNotificationHandlerTests()
    {
        _loggerMock = new Mock<ILogger<StateNotificationHandler>>();
        _muteServiceMock = new Mock<IManualMuteService>();
        _settingsServiceMock = new Mock<ISettingsService>();
        _statusUpdaterMock = new Mock<IServiceStatusUpdater>();
        _iconCoordinatorMock = new Mock<ITrayIconCoordinator>();
        _iconAnimationServiceMock = new Mock<IIconAnimationService>();
        _lifecycleManagerMock = new Mock<IServiceLifecycleManager>();
        _dictationStateMachineMock = new Mock<IDictationStateMachine>();
        _recordingNotificationServiceMock = new Mock<IRecordingNotificationService>();
        _recordingOverlayServiceMock = new Mock<IRecordingOverlayService>();
        _recordingStartSoundPlayerMock = new Mock<ISoundEffectPlayer>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new StateNotificationHandler(
                null!,
                _muteServiceMock.Object,
                _settingsServiceMock.Object,
                _statusUpdaterMock.Object,
                _iconCoordinatorMock.Object,
                _iconAnimationServiceMock.Object));
        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullMuteService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new StateNotificationHandler(
                _loggerMock.Object,
                null!,
                _settingsServiceMock.Object,
                _statusUpdaterMock.Object,
                _iconCoordinatorMock.Object,
                _iconAnimationServiceMock.Object));
        Assert.Equal("muteService", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullSettingsService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new StateNotificationHandler(
                _loggerMock.Object,
                _muteServiceMock.Object,
                null!,
                _statusUpdaterMock.Object,
                _iconCoordinatorMock.Object,
                _iconAnimationServiceMock.Object));
        Assert.Equal("settingsService", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullStatusUpdater_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new StateNotificationHandler(
                _loggerMock.Object,
                _muteServiceMock.Object,
                _settingsServiceMock.Object,
                null!,
                _iconCoordinatorMock.Object,
                _iconAnimationServiceMock.Object));
        Assert.Equal("statusUpdater", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullIconCoordinator_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new StateNotificationHandler(
                _loggerMock.Object,
                _muteServiceMock.Object,
                _settingsServiceMock.Object,
                _statusUpdaterMock.Object,
                null!,
                _iconAnimationServiceMock.Object));
        Assert.Equal("iconCoordinator", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullIconAnimationService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new StateNotificationHandler(
                _loggerMock.Object,
                _muteServiceMock.Object,
                _settingsServiceMock.Object,
                _statusUpdaterMock.Object,
                _iconCoordinatorMock.Object,
                null!));
        Assert.Equal("iconAnimationService", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullOptionalDependencies_DoesNotThrow()
    {
        // Act & Assert - should not throw
        var handler = new StateNotificationHandler(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            _statusUpdaterMock.Object,
            _iconCoordinatorMock.Object,
            _iconAnimationServiceMock.Object,
            null, // lifecycleManager
            null, // dictationStateMachine
            null); // dictationWorker
        Assert.NotNull(handler);
    }

    #endregion

    #region SubscribeToEvents Tests

    [Fact]
    public void SubscribeToEvents_SubscribesToMuteStateChanged()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        handler.SubscribeToEvents();

        // Assert - verify event subscription by raising the event
        _muteServiceMock.Raise(m => m.MuteStateChanged += null, this, true);
        _iconCoordinatorMock.Verify(x => x.UpdateCenterIcon(true), Times.Once);
    }

    [Fact]
    public void SubscribeToEvents_WithDictationStateMachine_SubscribesToStateChanged()
    {
        // Arrange
        var handler = new StateNotificationHandler(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            _statusUpdaterMock.Object,
            _iconCoordinatorMock.Object,
            _iconAnimationServiceMock.Object,
            null,
            _dictationStateMachineMock.Object,
            null);

        // Act
        handler.SubscribeToEvents();

        // Assert - verify event subscription by raising the event
        _dictationStateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Recording);
        _iconAnimationServiceMock.Verify(x => x.HandleDictationStateChange(DictationState.Recording), Times.Once);
    }

    [Fact]
    public void SubscribeToEvents_WithoutDictationStateMachine_DoesNotThrow()
    {
        // Arrange
        var handler = CreateHandler();

        // Act & Assert - should not throw
        handler.SubscribeToEvents();
    }

    #endregion

    #region UnsubscribeFromEvents Tests

    [Fact]
    public void UnsubscribeFromEvents_UnsubscribesFromMuteStateChanged()
    {
        // Arrange
        var handler = CreateHandler();
        handler.SubscribeToEvents();

        // Act
        handler.UnsubscribeFromEvents();

        // Assert - verify unsubscription by raising event after unsubscribe
        _muteServiceMock.Raise(m => m.MuteStateChanged += null, this, true);
        _iconCoordinatorMock.Verify(x => x.UpdateCenterIcon(It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void UnsubscribeFromEvents_WithDictationStateMachine_UnsubscribesFromStateChanged()
    {
        // Arrange
        var handler = new StateNotificationHandler(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            _statusUpdaterMock.Object,
            _iconCoordinatorMock.Object,
            _iconAnimationServiceMock.Object,
            null,
            _dictationStateMachineMock.Object,
            null);
        handler.SubscribeToEvents();

        // Act
        handler.UnsubscribeFromEvents();

        // Assert - verify unsubscription by raising event after unsubscribe
        _dictationStateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Recording);
        _iconAnimationServiceMock.Verify(x => x.HandleDictationStateChange(It.IsAny<DictationState>()), Times.Never);
    }

    #endregion

    #region InitializeStatesAsync Tests

    [Fact]
    public async Task InitializeStatesAsync_UpdatesMuteState()
    {
        // Arrange
        _muteServiceMock.Setup(x => x.IsMuted).Returns(true);
        _settingsServiceMock.Setup(x => x.GetAsync("tts.muted", false)).ReturnsAsync(false);
        var handler = CreateHandler();

        // Act
        await handler.InitializeStatesAsync();

        // Assert
        _statusUpdaterMock.Verify(x => x.UpdateMuteState(true), Times.Once);
    }

    [Fact]
    public async Task InitializeStatesAsync_InitializesDictationAsEnabled()
    {
        // Arrange
        _muteServiceMock.Setup(x => x.IsMuted).Returns(false);
        _settingsServiceMock.Setup(x => x.GetAsync("tts.muted", false)).ReturnsAsync(false);
        var handler = CreateHandler();

        // Act
        await handler.InitializeStatesAsync();

        // Assert
        _statusUpdaterMock.Verify(x => x.UpdateDictationStatus(true), Times.Once);
    }

    [Fact]
    public async Task InitializeStatesAsync_WithoutDictationWorker_DoesNotThrow()
    {
        // Arrange
        _muteServiceMock.Setup(x => x.IsMuted).Returns(false);
        _settingsServiceMock.Setup(x => x.GetAsync("tts.muted", false)).ReturnsAsync(false);
        var handler = CreateHandler();

        // Act & Assert - should not throw
        await handler.InitializeStatesAsync();
    }

    [Fact]
    public async Task InitializeStatesAsync_UpdatesTtsMuteState()
    {
        // Arrange
        _muteServiceMock.Setup(x => x.IsMuted).Returns(false);
        _settingsServiceMock.Setup(x => x.GetAsync("tts.muted", false)).ReturnsAsync(true);
        var handler = CreateHandler();

        // Act
        await handler.InitializeStatesAsync();

        // Assert
        _statusUpdaterMock.Verify(x => x.UpdateTtsMuteState(true), Times.Once);
    }

    [Fact]
    public async Task InitializeStatesAsync_WithLifecycleManager_RefreshesServiceStatus()
    {
        // Arrange
        _muteServiceMock.Setup(x => x.IsMuted).Returns(false);
        _settingsServiceMock.Setup(x => x.GetAsync("tts.muted", false)).ReturnsAsync(false);
        var handler = new StateNotificationHandler(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            _statusUpdaterMock.Object,
            _iconCoordinatorMock.Object,
            _iconAnimationServiceMock.Object,
            _lifecycleManagerMock.Object,
            null,
            null);

        // Act
        await handler.InitializeStatesAsync();

        // Assert
        // NOTE: RefreshSpeechToTextStatusAsync verification removed (issue #466) - STT runs inline now
        _lifecycleManagerMock.Verify(x => x.RefreshLogViewerStatusAsync(), Times.Once);
    }

    [Fact]
    public async Task InitializeStatesAsync_WithoutLifecycleManager_DoesNotRefreshServices()
    {
        // Arrange
        _muteServiceMock.Setup(x => x.IsMuted).Returns(false);
        _settingsServiceMock.Setup(x => x.GetAsync("tts.muted", false)).ReturnsAsync(false);
        var handler = CreateHandler();

        // Act
        await handler.InitializeStatesAsync();

        // Assert - no exception, lifecycle manager not called
        // NOTE: RefreshSpeechToTextStatusAsync verification removed (issue #466) - STT runs inline now
    }

    [Fact]
    public async Task InitializeStatesAsync_WhenExceptionThrown_LogsErrorAndRethrows()
    {
        // Arrange
        _muteServiceMock.Setup(x => x.IsMuted).Returns(false);
        var exception = new InvalidOperationException("Settings error");
        _settingsServiceMock.Setup(x => x.GetAsync("tts.muted", false)).ThrowsAsync(exception);
        var handler = CreateHandler();

        // Act & Assert
        var thrownException = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.InitializeStatesAsync());
        Assert.Equal("Settings error", thrownException.Message);
    }

    #endregion

    #region OnMuteStateChanged Tests

    [Fact]
    public void OnMuteStateChanged_UpdatesIconCoordinator()
    {
        // Arrange
        var handler = CreateHandler();
        handler.SubscribeToEvents();

        // Act - simulate mute state changed event
        _muteServiceMock.Raise(m => m.MuteStateChanged += null, this, true);

        // Assert
        _iconCoordinatorMock.Verify(x => x.UpdateCenterIcon(true), Times.Once);
    }

    [Fact]
    public void OnMuteStateChanged_UpdatesStatusUpdater()
    {
        // Arrange
        var handler = CreateHandler();
        handler.SubscribeToEvents();

        // Act - simulate mute state changed event
        _muteServiceMock.Raise(m => m.MuteStateChanged += null, this, false);

        // Assert
        _statusUpdaterMock.Verify(x => x.UpdateMuteState(false), Times.Once);
    }

    #endregion

    #region OnDictationStateChanged Tests

    [Fact]
    public void OnDictationStateChanged_DelegatesToIconAnimationService()
    {
        // Arrange
        var handler = new StateNotificationHandler(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            _statusUpdaterMock.Object,
            _iconCoordinatorMock.Object,
            _iconAnimationServiceMock.Object,
            null,
            _dictationStateMachineMock.Object,
            null);
        handler.SubscribeToEvents();

        // Act - simulate dictation state changed event
        _dictationStateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Transcribing);

        // Assert
        _iconAnimationServiceMock.Verify(x => x.HandleDictationStateChange(DictationState.Transcribing), Times.Once);
    }

    [Fact]
    public void OnDictationStateChanged_WithIdleState_DelegatesToIconAnimationService()
    {
        // Arrange
        var handler = new StateNotificationHandler(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            _statusUpdaterMock.Object,
            _iconCoordinatorMock.Object,
            _iconAnimationServiceMock.Object,
            null,
            _dictationStateMachineMock.Object,
            null);
        handler.SubscribeToEvents();

        // Act - simulate dictation state changed event
        _dictationStateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Idle);

        // Assert
        _iconAnimationServiceMock.Verify(x => x.HandleDictationStateChange(DictationState.Idle), Times.Once);
    }

    [Fact]
    public void OnDictationStateChanged_WithRecordingState_CallsShowRecordingAsync()
    {
        // Arrange
        var handler = new StateNotificationHandler(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            _statusUpdaterMock.Object,
            _iconCoordinatorMock.Object,
            _iconAnimationServiceMock.Object,
            null,
            _dictationStateMachineMock.Object,
            null,
            _recordingNotificationServiceMock.Object);
        handler.SubscribeToEvents();

        // Act - simulate dictation state changed event
        _dictationStateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Recording);

        // Assert - give async void time to complete
        Thread.Sleep(50);
        _recordingNotificationServiceMock.Verify(x => x.ShowRecordingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OnDictationStateChanged_WithTranscribingState_CallsShowTranscribingAsync()
    {
        // Arrange
        var handler = new StateNotificationHandler(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            _statusUpdaterMock.Object,
            _iconCoordinatorMock.Object,
            _iconAnimationServiceMock.Object,
            null,
            _dictationStateMachineMock.Object,
            null,
            _recordingNotificationServiceMock.Object);
        handler.SubscribeToEvents();

        // Act - simulate dictation state changed event
        _dictationStateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Transcribing);

        // Assert - give async void time to complete
        Thread.Sleep(50);
        _recordingNotificationServiceMock.Verify(x => x.ShowTranscribingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OnDictationStateChanged_WithIdleState_CallsHideAsync()
    {
        // Arrange
        var handler = new StateNotificationHandler(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            _statusUpdaterMock.Object,
            _iconCoordinatorMock.Object,
            _iconAnimationServiceMock.Object,
            null,
            _dictationStateMachineMock.Object,
            null,
            _recordingNotificationServiceMock.Object);
        handler.SubscribeToEvents();

        // Act - simulate dictation state changed event
        _dictationStateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Idle);

        // Assert - give async void time to complete
        Thread.Sleep(50);
        _recordingNotificationServiceMock.Verify(x => x.HideAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OnDictationStateChanged_WithoutRecordingNotificationService_DoesNotThrow()
    {
        // Arrange
        var handler = new StateNotificationHandler(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            _statusUpdaterMock.Object,
            _iconCoordinatorMock.Object,
            _iconAnimationServiceMock.Object,
            null,
            _dictationStateMachineMock.Object,
            null,
            null); // No recording notification service
        handler.SubscribeToEvents();

        // Act & Assert - should not throw
        _dictationStateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Recording);
        _dictationStateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Transcribing);
        _dictationStateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Idle);
    }

    [Fact]
    public void OnDictationStateChanged_WithOverlayService_CallsOverlayInsteadOfNotification()
    {
        // Arrange
        var handler = new StateNotificationHandler(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            _statusUpdaterMock.Object,
            _iconCoordinatorMock.Object,
            _iconAnimationServiceMock.Object,
            null,
            _dictationStateMachineMock.Object,
            null,
            _recordingNotificationServiceMock.Object,
            _recordingOverlayServiceMock.Object);
        handler.SubscribeToEvents();

        // Act - simulate dictation state changed event
        _dictationStateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Recording);

        // Assert - give async void time to complete
        Thread.Sleep(50);
        _recordingOverlayServiceMock.Verify(x => x.ShowRecordingAsync(It.IsAny<CancellationToken>()), Times.Once);
        _recordingNotificationServiceMock.Verify(x => x.ShowRecordingAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void OnDictationStateChanged_WhenOverlayFails_FallsBackToNotification()
    {
        // Arrange
        _recordingOverlayServiceMock
            .Setup(x => x.ShowRecordingAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Overlay failed"));

        var handler = new StateNotificationHandler(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            _statusUpdaterMock.Object,
            _iconCoordinatorMock.Object,
            _iconAnimationServiceMock.Object,
            null,
            _dictationStateMachineMock.Object,
            null,
            _recordingNotificationServiceMock.Object,
            _recordingOverlayServiceMock.Object);
        handler.SubscribeToEvents();

        // Act - simulate dictation state changed event
        _dictationStateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Recording);

        // Assert - give async void time to complete
        Thread.Sleep(100);
        _recordingOverlayServiceMock.Verify(x => x.ShowRecordingAsync(It.IsAny<CancellationToken>()), Times.Once);
        _recordingNotificationServiceMock.Verify(x => x.ShowRecordingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OnDictationStateChanged_TranscribingState_CallsOverlayShowTranscribing()
    {
        // Arrange
        var handler = new StateNotificationHandler(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            _statusUpdaterMock.Object,
            _iconCoordinatorMock.Object,
            _iconAnimationServiceMock.Object,
            null,
            _dictationStateMachineMock.Object,
            null,
            _recordingNotificationServiceMock.Object,
            _recordingOverlayServiceMock.Object);
        handler.SubscribeToEvents();

        // Act
        _dictationStateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Transcribing);

        // Assert
        Thread.Sleep(50);
        _recordingOverlayServiceMock.Verify(x => x.ShowTranscribingAsync(It.IsAny<CancellationToken>()), Times.Once);
        _recordingNotificationServiceMock.Verify(x => x.ShowTranscribingAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void OnDictationStateChanged_IdleState_CallsOverlayHide()
    {
        // Arrange
        var handler = new StateNotificationHandler(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            _statusUpdaterMock.Object,
            _iconCoordinatorMock.Object,
            _iconAnimationServiceMock.Object,
            null,
            _dictationStateMachineMock.Object,
            null,
            _recordingNotificationServiceMock.Object,
            _recordingOverlayServiceMock.Object);
        handler.SubscribeToEvents();

        // Act
        _dictationStateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Idle);

        // Assert
        Thread.Sleep(50);
        _recordingOverlayServiceMock.Verify(x => x.HideAsync(It.IsAny<CancellationToken>()), Times.Once);
        _recordingNotificationServiceMock.Verify(x => x.HideAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void OnDictationStateChanged_WhenOverlayFailsOnTranscribing_FallsBackToNotification()
    {
        // Arrange
        _recordingOverlayServiceMock
            .Setup(x => x.ShowTranscribingAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Overlay failed"));

        var handler = new StateNotificationHandler(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            _statusUpdaterMock.Object,
            _iconCoordinatorMock.Object,
            _iconAnimationServiceMock.Object,
            null,
            _dictationStateMachineMock.Object,
            null,
            _recordingNotificationServiceMock.Object,
            _recordingOverlayServiceMock.Object);
        handler.SubscribeToEvents();

        // Act
        _dictationStateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Transcribing);

        // Assert
        Thread.Sleep(100);
        _recordingOverlayServiceMock.Verify(x => x.ShowTranscribingAsync(It.IsAny<CancellationToken>()), Times.Once);
        _recordingNotificationServiceMock.Verify(x => x.ShowTranscribingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OnDictationStateChanged_WhenOverlayFailsAndNoNotificationService_HandlesGracefully()
    {
        // Arrange
        _recordingOverlayServiceMock
            .Setup(x => x.ShowRecordingAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Overlay failed"));

        var handler = new StateNotificationHandler(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            _statusUpdaterMock.Object,
            _iconCoordinatorMock.Object,
            _iconAnimationServiceMock.Object,
            null,
            _dictationStateMachineMock.Object,
            null,
            null, // No notification service
            _recordingOverlayServiceMock.Object);
        handler.SubscribeToEvents();

        // Act - should not throw
        _dictationStateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Recording);

        // Assert - give async void time to complete
        Thread.Sleep(100);
        _recordingOverlayServiceMock.Verify(x => x.ShowRecordingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OnDictationStateChanged_WithOnlyOverlayService_WorksWithoutNotificationService()
    {
        // Arrange
        var handler = new StateNotificationHandler(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            _statusUpdaterMock.Object,
            _iconCoordinatorMock.Object,
            _iconAnimationServiceMock.Object,
            null,
            _dictationStateMachineMock.Object,
            null,
            null, // No notification service
            _recordingOverlayServiceMock.Object);
        handler.SubscribeToEvents();

        // Act
        _dictationStateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Recording);

        // Assert
        Thread.Sleep(50);
        _recordingOverlayServiceMock.Verify(x => x.ShowRecordingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void OnDictationStateChanged_WithRecordingState_PlaysRecordingStartSound()
    {
        // Arrange
        var handler = new StateNotificationHandler(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            _statusUpdaterMock.Object,
            _iconCoordinatorMock.Object,
            _iconAnimationServiceMock.Object,
            null,
            _dictationStateMachineMock.Object,
            null,
            null,
            null,
            _recordingStartSoundPlayerMock.Object);
        handler.SubscribeToEvents();

        // Act - simulate dictation state changed to Recording
        _dictationStateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Recording);

        // Assert
        _recordingStartSoundPlayerMock.Verify(x => x.Play(), Times.Once);
    }

    [Fact]
    public void OnDictationStateChanged_WithTranscribingState_DoesNotPlayRecordingStartSound()
    {
        // Arrange
        var handler = new StateNotificationHandler(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            _statusUpdaterMock.Object,
            _iconCoordinatorMock.Object,
            _iconAnimationServiceMock.Object,
            null,
            _dictationStateMachineMock.Object,
            null,
            null,
            null,
            _recordingStartSoundPlayerMock.Object);
        handler.SubscribeToEvents();

        // Act - simulate dictation state changed to Transcribing
        _dictationStateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Transcribing);

        // Assert - sound should NOT play for Transcribing state
        _recordingStartSoundPlayerMock.Verify(x => x.Play(), Times.Never);
    }

    [Fact]
    public void OnDictationStateChanged_WithIdleState_DoesNotPlayRecordingStartSound()
    {
        // Arrange
        var handler = new StateNotificationHandler(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            _statusUpdaterMock.Object,
            _iconCoordinatorMock.Object,
            _iconAnimationServiceMock.Object,
            null,
            _dictationStateMachineMock.Object,
            null,
            null,
            null,
            _recordingStartSoundPlayerMock.Object);
        handler.SubscribeToEvents();

        // Act - simulate dictation state changed to Idle
        _dictationStateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Idle);

        // Assert - sound should NOT play for Idle state
        _recordingStartSoundPlayerMock.Verify(x => x.Play(), Times.Never);
    }

    #endregion

    private StateNotificationHandler CreateHandler()
    {
        return new StateNotificationHandler(
            _loggerMock.Object,
            _muteServiceMock.Object,
            _settingsServiceMock.Object,
            _statusUpdaterMock.Object,
            _iconCoordinatorMock.Object,
            _iconAnimationServiceMock.Object);
    }
}
