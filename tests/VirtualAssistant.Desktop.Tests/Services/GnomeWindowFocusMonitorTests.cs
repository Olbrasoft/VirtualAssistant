using Microsoft.Extensions.Logging;
using Moq;
using VirtualAssistant.Desktop.Events;
using VirtualAssistant.Desktop.Models;
using VirtualAssistant.Desktop.Services;

namespace VirtualAssistant.Desktop.Tests.Services;

public class GnomeWindowFocusMonitorTests
{
    private readonly Mock<ILogger<GnomeWindowFocusMonitor>> _loggerMock;

    public GnomeWindowFocusMonitorTests()
    {
        _loggerMock = new Mock<ILogger<GnomeWindowFocusMonitor>>();
    }

    [Fact]
    public void ParseFocusedWindow_ValidJson_ReturnsCorrectWindow()
    {
        // Arrange
        var monitor = new GnomeWindowFocusMonitor(_loggerMock.Object);
        var gdbusOutput = @"('[{""id"": 195638082, ""wm_class"": ""kitty"", ""title"": ""Terminal"", ""focus"": true}, {""id"": 195638109, ""wm_class"": ""microsoft-edge"", ""title"": ""GitHub"", ""focus"": false}]',)";

        // Act
        var result = monitor.ParseFocusedWindow(gdbusOutput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(195638082u, result.Id);
        Assert.Equal("kitty", result.WmClass);
        Assert.Equal("Terminal", result.Title);
    }

    [Fact]
    public void ParseFocusedWindow_NoFocusedWindow_ReturnsNull()
    {
        // Arrange
        var monitor = new GnomeWindowFocusMonitor(_loggerMock.Object);
        var gdbusOutput = @"('[{""id"": 195638082, ""wm_class"": ""kitty"", ""title"": ""Terminal"", ""focus"": false}]',)";

        // Act
        var result = monitor.ParseFocusedWindow(gdbusOutput);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ParseFocusedWindow_EmptyArray_ReturnsNull()
    {
        // Arrange
        var monitor = new GnomeWindowFocusMonitor(_loggerMock.Object);
        var gdbusOutput = @"('[]',)";

        // Act
        var result = monitor.ParseFocusedWindow(gdbusOutput);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ParseFocusedWindow_InvalidJson_ReturnsNull()
    {
        // Arrange
        var monitor = new GnomeWindowFocusMonitor(_loggerMock.Object);
        var gdbusOutput = "invalid output";

        // Act
        var result = monitor.ParseFocusedWindow(gdbusOutput);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ParseFocusedWindow_MultipleFocused_ReturnsFirst()
    {
        // Arrange
        var monitor = new GnomeWindowFocusMonitor(_loggerMock.Object);
        var gdbusOutput = @"('[{""id"": 1, ""wm_class"": ""first"", ""title"": ""First"", ""focus"": true}, {""id"": 2, ""wm_class"": ""second"", ""title"": ""Second"", ""focus"": true}]',)";

        // Act
        var result = monitor.ParseFocusedWindow(gdbusOutput);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1u, result.Id);
        Assert.Equal("first", result.WmClass);
    }

    [Fact]
    public void Constructor_SetsDefaultPollingInterval()
    {
        // Arrange & Act
        var monitor = new GnomeWindowFocusMonitor(_loggerMock.Object);

        // Assert
        Assert.Null(monitor.CurrentWindow);
        Assert.Null(monitor.PreviousWindow);
    }

    [Fact]
    public void Constructor_CustomPollingInterval_IsAccepted()
    {
        // Arrange & Act
        var monitor = new GnomeWindowFocusMonitor(_loggerMock.Object, TimeSpan.FromSeconds(1));

        // Assert - no exception means success
        Assert.NotNull(monitor);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var monitor = new GnomeWindowFocusMonitor(_loggerMock.Object);

        // Act & Assert - should not throw
        monitor.Dispose();
        monitor.Dispose();
    }
}

public class WindowInfoTests
{
    [Fact]
    public void WindowInfo_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var time = DateTime.Now;
        var window1 = new WindowInfo(1, "kitty", "Terminal", time);
        var window2 = new WindowInfo(1, "kitty", "Terminal", time);
        var window3 = new WindowInfo(2, "kitty", "Terminal", time);

        // Assert
        Assert.Equal(window1, window2);
        Assert.NotEqual(window1, window3);
    }
}

public class WindowFocusChangedEventArgsTests
{
    [Fact]
    public void EventArgs_StoresWindows_Correctly()
    {
        // Arrange
        var previous = new WindowInfo(1, "kitty", "Terminal", DateTime.Now);
        var current = new WindowInfo(2, "edge", "Browser", DateTime.Now);

        // Act
        var args = new WindowFocusChangedEventArgs(previous, current);

        // Assert
        Assert.Equal(previous, args.PreviousWindow);
        Assert.Equal(current, args.CurrentWindow);
    }

    [Fact]
    public void EventArgs_NullPrevious_IsAllowed()
    {
        // Arrange
        var current = new WindowInfo(1, "kitty", "Terminal", DateTime.Now);

        // Act
        var args = new WindowFocusChangedEventArgs(null, current);

        // Assert
        Assert.Null(args.PreviousWindow);
        Assert.Equal(current, args.CurrentWindow);
    }
}
