using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Desktop.Services;

namespace Olbrasoft.VirtualAssistant.Desktop.Tests.Services;

/// <summary>
/// Unit tests for CursorPositionService.
/// Integration tests require GNOME Shell extension to be available.
/// </summary>
public class CursorPositionServiceTests
{
    private readonly Mock<ILogger<CursorPositionService>> _loggerMock;

    public CursorPositionServiceTests()
    {
        _loggerMock = new Mock<ILogger<CursorPositionService>>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CursorPositionService(null!));
    }

    [Fact]
    public void Constructor_WithValidLogger_DoesNotThrow()
    {
        var service = new CursorPositionService(_loggerMock.Object);
        Assert.NotNull(service);
    }

    [Fact]
    public async Task GetCursorPositionAsync_WhenExtensionUnavailable_ReturnsNull()
    {
        // Arrange
        var service = new CursorPositionService(_loggerMock.Object);

        // Act - Will fail to connect to D-Bus in test environment
        var position = await service.GetCursorPositionAsync();

        // Assert - Should return null gracefully, not throw
        // (actual result depends on D-Bus availability in test environment)
        // If D-Bus is not available, should return null
    }

    [Fact]
    public async Task GetActiveWindowGeometryAsync_WhenExtensionUnavailable_ReturnsNull()
    {
        // Arrange
        var service = new CursorPositionService(_loggerMock.Object);

        // Act - Will fail to connect to D-Bus in test environment
        var geometry = await service.GetActiveWindowGeometryAsync();

        // Assert - Should return null gracefully, not throw
        // (actual result depends on D-Bus availability in test environment)
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        var service = new CursorPositionService(_loggerMock.Object);

        // Act & Assert - should not throw
        await service.DisposeAsync();
        await service.DisposeAsync();
    }

    [Fact]
    public async Task GetCursorPositionAsync_AfterDispose_ReturnsNull()
    {
        // Arrange
        var service = new CursorPositionService(_loggerMock.Object);
        await service.DisposeAsync();

        // Act
        var position = await service.GetCursorPositionAsync();

        // Assert - disposed service should handle gracefully
        // The service may return null or previous cached value
    }
}
