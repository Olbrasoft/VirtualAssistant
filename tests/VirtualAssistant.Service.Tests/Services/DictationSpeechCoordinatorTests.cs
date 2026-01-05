using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Service.Infrastructure;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Services;

/// <summary>
/// Unit tests for DictationSpeechCoordinator.
/// Tests TTS coordination during dictation state changes.
/// </summary>
public class DictationSpeechCoordinatorTests : IDisposable
{
    private readonly Mock<ILogger<DictationSpeechCoordinator>> _loggerMock;
    private readonly Mock<IDictationStateMachine> _stateMachineMock;
    private readonly Mock<ISpeechLockService> _lockServiceMock;
    private readonly Mock<IVirtualAssistantSpeaker> _speakerMock;
    private readonly DictationSpeechCoordinator _sut;

    public DictationSpeechCoordinatorTests()
    {
        _loggerMock = new Mock<ILogger<DictationSpeechCoordinator>>();
        _stateMachineMock = new Mock<IDictationStateMachine>();
        _lockServiceMock = new Mock<ISpeechLockService>();
        _speakerMock = new Mock<IVirtualAssistantSpeaker>();

        _sut = new DictationSpeechCoordinator(
            _loggerMock.Object,
            _stateMachineMock.Object,
            _lockServiceMock.Object,
            _speakerMock.Object);
    }

    public void Dispose()
    {
        _sut?.Dispose();
    }

    #region StartAsync Tests

    [Fact]
    public async Task StartAsync_SubscribesToStateChanges()
    {
        // Act
        await _sut.StartAsync(CancellationToken.None);

        // Assert - verify subscription by raising event
        _stateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Recording);

        // Should have locked TTS
        _lockServiceMock.Verify(x => x.Lock(It.IsAny<TimeSpan?>()), Times.Once);
    }

    #endregion

    #region State Change Tests - Recording

    [Fact]
    public async Task OnStateChanged_Recording_LocksTTS()
    {
        // Arrange
        await _sut.StartAsync(CancellationToken.None);

        // Act
        _stateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Recording);

        // Assert
        _lockServiceMock.Verify(x => x.Lock(It.IsAny<TimeSpan?>()), Times.Once);
    }

    [Fact]
    public async Task OnStateChanged_Recording_CancelsCurrentSpeech_WhenPlaying()
    {
        // Arrange
        await _sut.StartAsync(CancellationToken.None);
        _speakerMock.Setup(x => x.IsPlaying).Returns(true);  // Audio is playing (user can hear it)
        _speakerMock.Setup(x => x.QueueCount).Returns(3);

        // Act
        _stateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Recording);

        // Assert
        _speakerMock.Verify(x => x.CancelCurrentSpeech(), Times.Once);
    }

    [Fact]
    public async Task OnStateChanged_Recording_DoesNotCancelSpeech_WhenGenerating()
    {
        // Arrange - TTS is generating audio but not playing yet
        await _sut.StartAsync(CancellationToken.None);
        _speakerMock.Setup(x => x.IsSpeaking).Returns(true);   // Generation in progress
        _speakerMock.Setup(x => x.IsPlaying).Returns(false);   // But not playing yet

        // Act
        _stateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Recording);

        // Assert - Message should NOT be cancelled (will queue naturally due to lock)
        _speakerMock.Verify(x => x.CancelCurrentSpeech(), Times.Never);
    }

    [Fact]
    public async Task OnStateChanged_Recording_DoesNotCancelSpeech_WhenNotSpeaking()
    {
        // Arrange
        await _sut.StartAsync(CancellationToken.None);
        _speakerMock.Setup(x => x.IsSpeaking).Returns(false);
        _speakerMock.Setup(x => x.IsPlaying).Returns(false);

        // Act
        _stateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Recording);

        // Assert
        _speakerMock.Verify(x => x.CancelCurrentSpeech(), Times.Never);
    }

    #endregion

    #region State Change Tests - Transcribing

    [Fact]
    public async Task OnStateChanged_Transcribing_LocksTTS()
    {
        // Arrange
        await _sut.StartAsync(CancellationToken.None);

        // Act
        _stateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Transcribing);

        // Assert
        _lockServiceMock.Verify(x => x.Lock(It.IsAny<TimeSpan?>()), Times.Once);
    }

    [Fact]
    public async Task OnStateChanged_Transcribing_CancelsCurrentSpeech_WhenPlaying()
    {
        // Arrange
        await _sut.StartAsync(CancellationToken.None);
        _speakerMock.Setup(x => x.IsPlaying).Returns(true);  // Audio is playing

        // Act
        _stateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Transcribing);

        // Assert
        _speakerMock.Verify(x => x.CancelCurrentSpeech(), Times.Once);
    }

    [Fact]
    public async Task OnStateChanged_Transcribing_DoesNotCancelSpeech_WhenGenerating()
    {
        // Arrange - TTS is generating but not playing yet
        await _sut.StartAsync(CancellationToken.None);
        _speakerMock.Setup(x => x.IsSpeaking).Returns(true);   // Generation in progress
        _speakerMock.Setup(x => x.IsPlaying).Returns(false);   // But not playing yet

        // Act
        _stateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Transcribing);

        // Assert - Message should NOT be cancelled (will queue naturally)
        _speakerMock.Verify(x => x.CancelCurrentSpeech(), Times.Never);
    }

    #endregion

    #region State Change Tests - Idle

    [Fact]
    public async Task OnStateChanged_Idle_UnlocksTTS()
    {
        // Arrange
        await _sut.StartAsync(CancellationToken.None);
        _speakerMock.Setup(x => x.QueueCount).Returns(0);

        // Act
        _stateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Idle);

        // Give async handler time to complete
        await Task.Delay(100);

        // Assert
        _lockServiceMock.Verify(x => x.Unlock(), Times.Once);
    }

    [Fact]
    public async Task OnStateChanged_Idle_FlushesQueue_WhenMessagesQueued()
    {
        // Arrange
        await _sut.StartAsync(CancellationToken.None);
        _speakerMock.Setup(x => x.QueueCount).Returns(5);

        // Act
        _stateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Idle);

        // Give async handler time to complete
        await Task.Delay(100);

        // Assert
        _speakerMock.Verify(x => x.FlushQueueAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnStateChanged_Idle_DoesNotFlushQueue_WhenNoMessagesQueued()
    {
        // Arrange
        await _sut.StartAsync(CancellationToken.None);
        _speakerMock.Setup(x => x.QueueCount).Returns(0);

        // Act
        _stateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Idle);

        // Give async handler time to complete
        await Task.Delay(100);

        // Assert
        _speakerMock.Verify(x => x.FlushQueueAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region State Transition Scenarios

    [Fact]
    public async Task Scenario_UserStartsDictation_WhileTTSPlaying()
    {
        // Arrange - TTS is actually playing (user can hear it)
        await _sut.StartAsync(CancellationToken.None);
        _speakerMock.Setup(x => x.IsPlaying).Returns(true);    // Audio playing
        _speakerMock.Setup(x => x.QueueCount).Returns(2);

        // Act - user starts dictating (CapsLock ON)
        _stateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Recording);

        // Assert - Should cancel the playing message
        _lockServiceMock.Verify(x => x.Lock(It.IsAny<TimeSpan?>()), Times.Once);
        _speakerMock.Verify(x => x.CancelCurrentSpeech(), Times.Once);
    }

    [Fact]
    public async Task Scenario_UserStartsDictation_WhileTTSGenerating()
    {
        // Arrange - TTS is generating audio but not playing yet (1-2 sec generation phase)
        await _sut.StartAsync(CancellationToken.None);
        _speakerMock.Setup(x => x.IsSpeaking).Returns(true);   // Generation in progress
        _speakerMock.Setup(x => x.IsPlaying).Returns(false);   // Not playing yet
        _speakerMock.Setup(x => x.QueueCount).Returns(0);

        // Act - user starts dictating (CapsLock ON) during generation
        _stateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Recording);

        // Assert - Should NOT cancel (message will queue naturally due to lock)
        _lockServiceMock.Verify(x => x.Lock(It.IsAny<TimeSpan?>()), Times.Once);
        _speakerMock.Verify(x => x.CancelCurrentSpeech(), Times.Never);
    }

    [Fact]
    public async Task Scenario_UserCompletesDictation_WithQueuedMessages()
    {
        // Arrange
        await _sut.StartAsync(CancellationToken.None);

        // User starts dictating
        _stateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Recording);

        // During dictation, messages queue up
        _speakerMock.Setup(x => x.QueueCount).Returns(3);

        // Act - user completes dictation (CapsLock OFF → Idle)
        _stateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Idle);

        // Give async handler time to complete
        await Task.Delay(100);

        // Assert
        _lockServiceMock.Verify(x => x.Unlock(), Times.Once);
        _speakerMock.Verify(x => x.FlushQueueAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Scenario_FullDictationCycle_RecordingToTranscribingToIdle()
    {
        // Arrange
        await _sut.StartAsync(CancellationToken.None);
        _speakerMock.Setup(x => x.IsSpeaking).Returns(false);
        _speakerMock.Setup(x => x.QueueCount).Returns(0);

        // Act 1 - User starts recording (CapsLock ON)
        _stateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Recording);

        // Assert 1 - TTS locked
        _lockServiceMock.Verify(x => x.Lock(It.IsAny<TimeSpan?>()), Times.Once);

        // Act 2 - Recording stops, transcription starts
        _stateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Transcribing);

        // Assert 2 - TTS still locked
        _lockServiceMock.Verify(x => x.Lock(It.IsAny<TimeSpan?>()), Times.Exactly(2)); // Called again

        // Act 3 - Transcription completes
        _stateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Idle);

        // Give async handler time to complete
        await Task.Delay(100);

        // Assert 3 - TTS unlocked
        _lockServiceMock.Verify(x => x.Unlock(), Times.Once);
    }

    #endregion

    #region StopAsync Tests

    [Fact]
    public async Task StopAsync_UnsubscribesFromStateChanges()
    {
        // Arrange
        await _sut.StartAsync(CancellationToken.None);

        // Act
        await _sut.StopAsync(CancellationToken.None);

        // Assert - verify unsubscription by raising event (should not trigger lock)
        _stateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Recording);

        // Should NOT have locked TTS (unsubscribed)
        _lockServiceMock.Verify(x => x.Lock(It.IsAny<TimeSpan?>()), Times.Never);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public async Task Dispose_UnsubscribesFromStateChanges()
    {
        // Arrange
        await _sut.StartAsync(CancellationToken.None);

        // Act
        _sut.Dispose();

        // Assert - verify unsubscription by raising event (should not trigger lock)
        _stateMachineMock.Raise(m => m.StateChanged += null, this, DictationState.Recording);

        // Should NOT have locked TTS (unsubscribed)
        _lockServiceMock.Verify(x => x.Lock(It.IsAny<TimeSpan?>()), Times.Never);
    }

    #endregion
}
