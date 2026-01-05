using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Service.Infrastructure;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="IconAnimationService"/>.
/// Verifies correct hand icon animations based on dictation state changes.
/// </summary>
public class IconAnimationServiceTests
{
    private readonly Mock<ITrayIconCoordinator> _iconCoordinatorMock;
    private readonly Mock<ILogger<IconAnimationService>> _loggerMock;
    private readonly IconAnimationService _service;

    public IconAnimationServiceTests()
    {
        _iconCoordinatorMock = new Mock<ITrayIconCoordinator>();
        _loggerMock = new Mock<ILogger<IconAnimationService>>();

        _service = new IconAnimationService(
            _iconCoordinatorMock.Object,
            _loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullIconCoordinator_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new IconAnimationService(null!, _loggerMock.Object));
        Assert.Equal("iconCoordinator", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new IconAnimationService(_iconCoordinatorMock.Object, null!));
        Assert.Equal("logger", exception.ParamName);
    }

    #endregion

    #region HandleDictationStateChange - Idle State

    [Fact]
    public void HandleDictationStateChange_WhenIdle_SetsDefaultIcons()
    {
        // Arrange
        var state = DictationState.Idle;

        // Act
        _service.HandleDictationStateChange(state);

        // Assert
        _iconCoordinatorMock.Verify(
            x => x.SetRightHandIcon("default-right-hand.svg"),
            Times.Once);
        _iconCoordinatorMock.Verify(
            x => x.SetCenterIcon("default-head.svg"),
            Times.Once);
    }

    #endregion

    #region HandleDictationStateChange - Recording State

    [Fact]
    public void HandleDictationStateChange_WhenRecording_SetsDictationIcons()
    {
        // Arrange
        var state = DictationState.Recording;

        // Act
        _service.HandleDictationStateChange(state);

        // Assert
        _iconCoordinatorMock.Verify(
            x => x.SetRightHandIcon("holding-up-a-microphone-right-hand.svg"),
            Times.Once);
        _iconCoordinatorMock.Verify(
            x => x.SetCenterIcon("listening-dictation-head.svg"),
            Times.Once);
    }

    #endregion

    #region HandleDictationStateChange - Transcribing State

    [Fact]
    public void HandleDictationStateChange_WhenTranscribing_SetsTranscribingIcons()
    {
        // Arrange
        var state = DictationState.Transcribing;

        // Act
        _service.HandleDictationStateChange(state);

        // Assert
        _iconCoordinatorMock.Verify(
            x => x.SetRightHandIcon("writing-right-hand.svg"),
            Times.Once);
        _iconCoordinatorMock.Verify(
            x => x.SetCenterIcon("busy-head.svg"),
            Times.Once);
    }

    #endregion

    #region HandleDictationStateChange - Error Handling

    [Fact]
    public void HandleDictationStateChange_WhenIconCoordinatorThrows_LogsError()
    {
        // Arrange
        var state = DictationState.Recording;
        var expectedException = new InvalidOperationException("Icon update failed");

        _iconCoordinatorMock
            .Setup(x => x.SetRightHandIcon(It.IsAny<string>()))
            .Throws(expectedException);

        // Act
        _service.HandleDictationStateChange(state);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to update icon")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region HandleDictationStateChange - State Transitions

    [Fact]
    public void HandleDictationStateChange_MultipleStateChanges_UpdatesIconCorrectly()
    {
        // Arrange & Act - Simulate full dictation cycle
        _service.HandleDictationStateChange(DictationState.Idle);
        _service.HandleDictationStateChange(DictationState.Recording);
        _service.HandleDictationStateChange(DictationState.Transcribing);
        _service.HandleDictationStateChange(DictationState.Idle);

        // Assert
        _iconCoordinatorMock.Verify(
            x => x.SetRightHandIcon("default-right-hand.svg"),
            Times.Exactly(2)); // Idle at start and end

        _iconCoordinatorMock.Verify(
            x => x.SetRightHandIcon("holding-up-a-microphone-right-hand.svg"),
            Times.Once); // Recording

        _iconCoordinatorMock.Verify(
            x => x.SetRightHandIcon("writing-right-hand.svg"),
            Times.Once); // Transcribing

        _iconCoordinatorMock.Verify(
            x => x.SetCenterIcon("default-head.svg"),
            Times.Exactly(2)); // Idle at start and end

        _iconCoordinatorMock.Verify(
            x => x.SetCenterIcon("listening-dictation-head.svg"),
            Times.Once); // Recording

        _iconCoordinatorMock.Verify(
            x => x.SetCenterIcon("busy-head.svg"),
            Times.Once); // Transcribing
    }

    #endregion
}
