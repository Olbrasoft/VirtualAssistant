using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Desktop.Services;

namespace Olbrasoft.VirtualAssistant.Desktop.Tests.Services;

/// <summary>
/// Unit tests for RecordingOverlayService.
/// </summary>
public class RecordingOverlayServiceTests
{
    private readonly Mock<ILogger<RecordingOverlayService>> _loggerMock;
    private readonly Mock<IRecordingNotificationService> _notificationServiceMock;
    private readonly Mock<ICursorPositionService> _cursorPositionServiceMock;

    public RecordingOverlayServiceTests()
    {
        _loggerMock = new Mock<ILogger<RecordingOverlayService>>();
        _notificationServiceMock = new Mock<IRecordingNotificationService>();
        _cursorPositionServiceMock = new Mock<ICursorPositionService>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RecordingOverlayService(null!, _notificationServiceMock.Object, _cursorPositionServiceMock.Object));
    }

    [Fact]
    public void Constructor_WithNullNotificationService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RecordingOverlayService(_loggerMock.Object, null!, _cursorPositionServiceMock.Object));
    }

    [Fact]
    public void Constructor_WithNullCursorPositionService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RecordingOverlayService(_loggerMock.Object, _notificationServiceMock.Object, null!));
    }

    [Fact]
    public async Task ShowRecordingAsync_DelegatesToNotificationService()
    {
        // Arrange
        _cursorPositionServiceMock
            .Setup(x => x.GetCursorPositionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((100, 200));

        var service = CreateService();

        // Act
        await service.ShowRecordingAsync();

        // Assert
        _notificationServiceMock.Verify(x => x.ShowRecordingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShowTranscribingAsync_DelegatesToNotificationService()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.ShowTranscribingAsync();

        // Assert
        _notificationServiceMock.Verify(x => x.ShowTranscribingAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HideAsync_DelegatesToNotificationService()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.HideAsync();

        // Assert
        _notificationServiceMock.Verify(x => x.HideAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShowRecordingAsync_QueriesCursorPosition()
    {
        // Arrange
        _cursorPositionServiceMock
            .Setup(x => x.GetCursorPositionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((100, 200));

        var service = CreateService();

        // Act
        await service.ShowRecordingAsync();

        // Assert
        _cursorPositionServiceMock.Verify(x => x.GetCursorPositionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdatePositionAsync_QueriesCursorPosition()
    {
        // Arrange
        _cursorPositionServiceMock
            .Setup(x => x.GetCursorPositionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((300, 400));

        var service = CreateService();

        // Act
        await service.UpdatePositionAsync();

        // Assert
        _cursorPositionServiceMock.Verify(x => x.GetCursorPositionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert - should not throw
        await service.DisposeAsync();
        await service.DisposeAsync();
    }

    [Fact]
    public async Task ShowRecordingAsync_AfterDispose_DoesNotThrow()
    {
        // Arrange
        var service = CreateService();
        await service.DisposeAsync();

        // Act & Assert - should not throw, just return early
        await service.ShowRecordingAsync();

        // Notification service should not be called after dispose
        _notificationServiceMock.Verify(x => x.ShowRecordingAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private RecordingOverlayService CreateService()
    {
        return new RecordingOverlayService(
            _loggerMock.Object,
            _notificationServiceMock.Object,
            _cursorPositionServiceMock.Object);
    }
}
