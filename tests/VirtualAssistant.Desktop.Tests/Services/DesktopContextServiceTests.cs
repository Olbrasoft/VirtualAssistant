using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using Moq;
using VirtualAssistant.Core.Models;
using VirtualAssistant.Desktop.Services;

namespace VirtualAssistant.Desktop.Tests.Services;

public class DesktopContextServiceTests : IAsyncDisposable
{
    private readonly Mock<ILogger<DesktopContextService>> _loggerMock;
    private bool _disposed;

    public DesktopContextServiceTests()
    {
        _loggerMock = new Mock<ILogger<DesktopContextService>>();
    }

    [Fact]
    public async Task GetCurrentContextAsync_WithValidMonitor_ReturnsContext()
    {
        // Arrange
        var expectedContext = new DesktopContext(
            CurrentWorkspace: 1,
            TotalWorkspaces: 4,
            ActiveWindowTitle: "Test Window",
            ActiveWindowClass: "test-app",
            ActiveApplication: "test-app",
            Timestamp: DateTime.UtcNow
        );

        var contextSubject = new Subject<DesktopContext>();
        var monitorMock = new Mock<IDesktopMonitorBackgroundService>();
        monitorMock.Setup(x => x.CurrentContext).Returns(expectedContext);
        monitorMock.Setup(x => x.IsAvailable).Returns(true);
        monitorMock.Setup(x => x.ContextUpdates).Returns(contextSubject);

        await using var sut = new DesktopContextService(
            monitorMock.Object,
            _loggerMock.Object);

        // Act
        var result = await sut.GetCurrentContextAsync();

        // Assert
        Assert.Equal(1, result.CurrentWorkspace);
        Assert.Equal(4, result.TotalWorkspaces);
        Assert.Equal("test-app", result.ActiveApplication);
        Assert.Equal("Test Window", result.ActiveWindowTitle);
    }

    [Fact]
    public async Task GetCurrentContextAsync_WithNullMonitor_ReturnsEmptyContext()
    {
        // Arrange
        await using var sut = new DesktopContextService(null, _loggerMock.Object);

        // Act
        var result = await sut.GetCurrentContextAsync();

        // Assert
        Assert.Equal("Unknown", result.ActiveApplication);
        Assert.Equal("Unknown", result.ActiveWindowTitle);
        Assert.Equal(0, result.CurrentWorkspace);

        // Verify warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("unavailable")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetCurrentContextAsync_WhenMonitorNotAvailable_ReturnsEmptyContext()
    {
        // Arrange
        var contextSubject = new Subject<DesktopContext>();
        var monitorMock = new Mock<IDesktopMonitorBackgroundService>();
        monitorMock.Setup(x => x.IsAvailable).Returns(false);
        monitorMock.Setup(x => x.CurrentContext).Returns((DesktopContext?)null);
        monitorMock.Setup(x => x.ContextUpdates).Returns(contextSubject);

        await using var sut = new DesktopContextService(
            monitorMock.Object,
            _loggerMock.Object);

        // Act
        var result = await sut.GetCurrentContextAsync();

        // Assert
        Assert.Equal("Unknown", result.ActiveApplication);

        // Verify warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("unavailable")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task IsAvailableAsync_WithAvailableMonitor_ReturnsTrue()
    {
        // Arrange
        var contextSubject = new Subject<DesktopContext>();
        var monitorMock = new Mock<IDesktopMonitorBackgroundService>();
        monitorMock.Setup(x => x.IsAvailable).Returns(true);
        monitorMock.Setup(x => x.ContextUpdates).Returns(contextSubject);

        await using var sut = new DesktopContextService(
            monitorMock.Object,
            _loggerMock.Object);

        // Act
        var result = await sut.IsAvailableAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsAvailableAsync_WithNullMonitor_ReturnsFalse()
    {
        // Arrange
        await using var sut = new DesktopContextService(null, _loggerMock.Object);

        // Act
        var result = await sut.IsAvailableAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsAvailableAsync_WhenMonitorNotAvailable_ReturnsFalse()
    {
        // Arrange
        var contextSubject = new Subject<DesktopContext>();
        var monitorMock = new Mock<IDesktopMonitorBackgroundService>();
        monitorMock.Setup(x => x.IsAvailable).Returns(false);
        monitorMock.Setup(x => x.ContextUpdates).Returns(contextSubject);

        await using var sut = new DesktopContextService(
            monitorMock.Object,
            _loggerMock.Object);

        // Act
        var result = await sut.IsAvailableAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ContextChanges_ReturnsObservable()
    {
        // Arrange
        var monitorMock = new Mock<IDesktopMonitorBackgroundService>();
        monitorMock.Setup(x => x.ContextUpdates).Returns(new Subject<DesktopContext>());

        await using var sut = new DesktopContextService(
            monitorMock.Object,
            _loggerMock.Object);

        // Act & Assert
        Assert.NotNull(sut.ContextChanges);
    }

    [Fact]
    public async Task ContextChanges_EmitsWorkspaceChange_WhenMonitorEmitsWorkspaceChange()
    {
        // Arrange
        var contextSubject = new Subject<DesktopContext>();
        var monitorMock = new Mock<IDesktopMonitorBackgroundService>();
        monitorMock.Setup(x => x.ContextUpdates).Returns(contextSubject);

        await using var sut = new DesktopContextService(
            monitorMock.Object,
            _loggerMock.Object);

        DesktopContextChange? capturedChange = null;
        sut.ContextChanges.Subscribe(change => capturedChange = change);

        var initialContext = new DesktopContext(0, 4, "Window 1", "app1", "app1", DateTime.UtcNow);
        var newContext = new DesktopContext(1, 4, "Window 1", "app1", "app1", DateTime.UtcNow);

        // Act - emit initial context, then workspace change
        contextSubject.OnNext(initialContext);
        contextSubject.OnNext(newContext);

        await Task.Delay(100); // Give time for processing

        // Assert
        Assert.NotNull(capturedChange);
        Assert.Equal(ChangeType.WorkspaceChanged, capturedChange.Type);
        Assert.Equal(0, capturedChange.PreviousContext.CurrentWorkspace);
        Assert.Equal(1, capturedChange.NewContext.CurrentWorkspace);
    }

    [Fact]
    public async Task ContextChanges_EmitsApplicationChange_WhenMonitorEmitsApplicationChange()
    {
        // Arrange
        var contextSubject = new Subject<DesktopContext>();
        var monitorMock = new Mock<IDesktopMonitorBackgroundService>();
        monitorMock.Setup(x => x.ContextUpdates).Returns(contextSubject);

        await using var sut = new DesktopContextService(
            monitorMock.Object,
            _loggerMock.Object);

        DesktopContextChange? capturedChange = null;
        sut.ContextChanges.Subscribe(change => capturedChange = change);

        var initialContext = new DesktopContext(0, 4, "Window 1", "app1", "app1", DateTime.UtcNow);
        var newContext = new DesktopContext(0, 4, "Window 2", "app2", "app2", DateTime.UtcNow);

        // Act
        contextSubject.OnNext(initialContext);
        contextSubject.OnNext(newContext);

        await Task.Delay(100);

        // Assert
        Assert.NotNull(capturedChange);
        Assert.Equal(ChangeType.ApplicationChanged, capturedChange.Type);
        Assert.Equal("app1", capturedChange.PreviousContext.ActiveApplication);
        Assert.Equal("app2", capturedChange.NewContext.ActiveApplication);
    }

    [Fact]
    public async Task ContextChanges_EmitsWindowFocusChange_WhenMonitorEmitsWindowTitleChange()
    {
        // Arrange
        var contextSubject = new Subject<DesktopContext>();
        var monitorMock = new Mock<IDesktopMonitorBackgroundService>();
        monitorMock.Setup(x => x.ContextUpdates).Returns(contextSubject);

        await using var sut = new DesktopContextService(
            monitorMock.Object,
            _loggerMock.Object);

        DesktopContextChange? capturedChange = null;
        sut.ContextChanges.Subscribe(change => capturedChange = change);

        var initialContext = new DesktopContext(0, 4, "Window 1", "app1", "app1", DateTime.UtcNow);
        var newContext = new DesktopContext(0, 4, "Window 2", "app1", "app1", DateTime.UtcNow);

        // Act
        contextSubject.OnNext(initialContext);
        contextSubject.OnNext(newContext);

        await Task.Delay(100);

        // Assert
        Assert.NotNull(capturedChange);
        Assert.Equal(ChangeType.WindowFocusChanged, capturedChange.Type);
        Assert.Equal("Window 1", capturedChange.PreviousContext.ActiveWindowTitle);
        Assert.Equal("Window 2", capturedChange.NewContext.ActiveWindowTitle);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        GC.SuppressFinalize(this);
        await ValueTask.CompletedTask;
    }
}
