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

    // NOTE: The following tests are commented out because they depend on GNOME Shell extension availability.
    // On local dev machine with GNOME extension installed, they return values.
    // On GitHub Actions (no GNOME/X11), they would return null.
    // This inconsistency makes them unreliable for CI/CD.

    // [Fact]
    // public async Task GetCursorPositionAsync_WhenExtensionUnavailable_ReturnsNull()
    // {
    //     var service = new CursorPositionService(_loggerMock.Object);
    //     var position = await service.GetCursorPositionAsync();
    //     Assert.Null(position);
    // }

    // [Fact]
    // public async Task GetActiveWindowGeometryAsync_WhenExtensionUnavailable_ReturnsNull()
    // {
    //     var service = new CursorPositionService(_loggerMock.Object);
    //     var geometry = await service.GetActiveWindowGeometryAsync();
    //     Assert.Null(geometry);
    // }

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

        // Assert - disposed service should return null gracefully
        Assert.Null(position);
    }
}
