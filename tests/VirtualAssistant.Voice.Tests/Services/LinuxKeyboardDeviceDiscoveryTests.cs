using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Tests.Services;

/// <summary>
/// Unit tests for <see cref="LinuxKeyboardDeviceDiscovery"/>.
/// Verifies keyboard device discovery logic.
/// </summary>
public class LinuxKeyboardDeviceDiscoveryTests
{
    private readonly Mock<ILogger<LinuxKeyboardDeviceDiscovery>> _loggerMock;

    public LinuxKeyboardDeviceDiscoveryTests()
    {
        _loggerMock = new Mock<ILogger<LinuxKeyboardDeviceDiscovery>>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new LinuxKeyboardDeviceDiscovery(null!));
        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Act
        var discovery = new LinuxKeyboardDeviceDiscovery(_loggerMock.Object);

        // Assert
        Assert.NotNull(discovery);
    }

    #endregion

    #region FindKeyboardDevice Tests

    [Fact]
    public void FindKeyboardDevice_WhenDirectoriesExist_ReturnsDevicePath()
    {
        // Arrange
        var discovery = new LinuxKeyboardDeviceDiscovery(_loggerMock.Object);

        // Act
        var result = discovery.FindKeyboardDevice();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        // Should return a device path (either by-id, eventX, or fallback)
        Assert.True(result.Contains("/dev/input/") || result.Contains("event"));
    }

    [Fact]
    public void FindKeyboardDevices_WhenDirectoriesExist_ReturnsUniqueDevicePaths()
    {
        // Arrange
        var discovery = new LinuxKeyboardDeviceDiscovery(_loggerMock.Object);

        // Act
        var result = discovery.FindKeyboardDevices();

        // Assert
        Assert.NotEmpty(result);
        Assert.Equal(result.Count, result.Distinct(StringComparer.Ordinal).Count());
        Assert.All(result, path => Assert.StartsWith("/dev/input/", path));
    }

    #endregion

    #region IsKeyboardDevice Tests

    [Fact]
    public void IsKeyboardDevice_WithNonExistentDevice_ReturnsFalse()
    {
        // Arrange
        var discovery = new LinuxKeyboardDeviceDiscovery(_loggerMock.Object);
        var nonExistentDevice = "/dev/input/event999";

        // Act
        var result = discovery.IsKeyboardDevice(nonExistentDevice);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsKeyboardDevice_WithInvalidPath_ReturnsFalse()
    {
        // Arrange
        var discovery = new LinuxKeyboardDeviceDiscovery(_loggerMock.Object);
        var invalidPath = "/invalid/path/to/device";

        // Act
        var result = discovery.IsKeyboardDevice(invalidPath);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Integration Tests (Skipped - filesystem dependent)

    [Fact(Skip = "Integration test - requires Linux /dev/input/ filesystem")]
    public void FindKeyboardDevice_OnLinuxSystem_FindsActualKeyboard()
    {
        // This test would require actual Linux filesystem
        // Skipped for unit tests
        Assert.True(true);
    }

    [Fact(Skip = "Integration test - requires Linux /sys/class/input/ filesystem")]
    public void IsKeyboardDevice_WithActualKeyboard_ReturnsTrue()
    {
        // This test would require actual Linux filesystem
        // Skipped for unit tests
        Assert.True(true);
    }

    [Fact(Skip = "Integration test - requires Linux /sys/class/input/ filesystem")]
    public void IsKeyboardDevice_WithNonKeyboardDevice_ReturnsFalse()
    {
        // This test would require actual Linux filesystem
        // Skipped for unit tests
        Assert.True(true);
    }

    #endregion
}
