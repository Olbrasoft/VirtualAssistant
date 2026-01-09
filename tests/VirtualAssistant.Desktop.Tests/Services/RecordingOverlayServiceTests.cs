using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Desktop.Services;
using Olbrasoft.VirtualAssistant.Desktop.UI;

namespace Olbrasoft.VirtualAssistant.Desktop.Tests.Services;

/// <summary>
/// Unit tests for RecordingOverlayService.
/// Uses IRecordingOverlayWindow mock to avoid GTK4 runtime dependency.
/// </summary>
public class RecordingOverlayServiceTests
{
    private readonly Mock<ILogger<RecordingOverlayService>> _loggerMock;
    private readonly Mock<ICursorPositionService> _cursorPositionServiceMock;
    private readonly Mock<IRecordingOverlayWindow> _overlayWindowMock;

    public RecordingOverlayServiceTests()
    {
        _loggerMock = new Mock<ILogger<RecordingOverlayService>>();
        _cursorPositionServiceMock = new Mock<ICursorPositionService>();
        _overlayWindowMock = new Mock<IRecordingOverlayWindow>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RecordingOverlayService(null!, _cursorPositionServiceMock.Object, _overlayWindowMock.Object));
    }

    [Fact]
    public void Constructor_WithNullCursorPositionService_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RecordingOverlayService(_loggerMock.Object, null!, _overlayWindowMock.Object));
    }

    [Fact]
    public void Constructor_WithNullOverlayWindow_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RecordingOverlayService(_loggerMock.Object, _cursorPositionServiceMock.Object, null!));
    }

    [Fact]
    public void Constructor_WithValidParameters_DoesNotThrow()
    {
        // Act & Assert - should not throw
        var service = CreateService();
        Assert.NotNull(service);
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

        // Assert - cursor position should have been queried
        _cursorPositionServiceMock.Verify(x => x.GetCursorPositionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _overlayWindowMock.Verify(x => x.Initialize(), Times.Once);
        _overlayWindowMock.Verify(x => x.ShowRecording(100, 200), Times.Once);
    }

    [Fact]
    public async Task ShowRecordingAsync_WithNullCursorPosition_UsesFallback()
    {
        // Arrange
        _cursorPositionServiceMock
            .Setup(x => x.GetCursorPositionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(((int, int)?)null);

        var service = CreateService();

        // Act
        await service.ShowRecordingAsync();

        // Assert - uses fallback position (100, 100)
        _cursorPositionServiceMock.Verify(x => x.GetCursorPositionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _overlayWindowMock.Verify(x => x.ShowRecording(100, 100), Times.Once);
    }

    [Fact]
    public async Task HideAsync_CallsOverlayWindowHide()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.HideAsync();

        // Assert
        _overlayWindowMock.Verify(x => x.Hide(), Times.Once);
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
        _overlayWindowMock.Verify(x => x.UpdatePosition(300, 400), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_DisposesOverlayWindow()
    {
        // Arrange
        var service = CreateService();

        // Act
        await service.DisposeAsync();

        // Assert
        _overlayWindowMock.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert - should not throw
        await service.DisposeAsync();
        await service.DisposeAsync();

        // Assert - dispose called only once
        _overlayWindowMock.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task ShowRecordingAsync_AfterDispose_ReturnsEarly()
    {
        // Arrange
        var service = CreateService();
        await service.DisposeAsync();

        // Act - should return early without calling cursor position service
        await service.ShowRecordingAsync();

        // Assert - cursor position service should not be called after dispose
        _cursorPositionServiceMock.Verify(x => x.GetCursorPositionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _overlayWindowMock.Verify(x => x.ShowRecording(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task HideAsync_AfterDispose_ReturnsEarly()
    {
        // Arrange
        var service = CreateService();
        await service.DisposeAsync();

        // Act
        await service.HideAsync();

        // Assert - hide not called on disposed service (besides initial dispose)
        _overlayWindowMock.Verify(x => x.Hide(), Times.Never);
    }

    [Fact]
    public async Task ShowTranscribingAsync_QueriesCursorAndShowsTranscribing()
    {
        // Arrange
        _cursorPositionServiceMock
            .Setup(x => x.GetCursorPositionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((150, 250));

        var service = CreateService();

        // Act
        await service.ShowTranscribingAsync();

        // Assert
        _cursorPositionServiceMock.Verify(x => x.GetCursorPositionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _overlayWindowMock.Verify(x => x.Initialize(), Times.Once);
        _overlayWindowMock.Verify(x => x.ShowTranscribing(150, 250), Times.Once);
    }

    private RecordingOverlayService CreateService()
    {
        return new RecordingOverlayService(
            _loggerMock.Object,
            _cursorPositionServiceMock.Object,
            _overlayWindowMock.Object);
    }
}
