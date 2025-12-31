using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Services;

namespace VirtualAssistant.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ServiceLifecycleManager"/>.
/// Verifies systemd service lifecycle management (start/stop/status).
/// </summary>
public class ServiceLifecycleManagerTests
{
    private readonly Mock<ILogger<ServiceLifecycleManager>> _loggerMock;
    private readonly Mock<ISpeechToTextServiceManager> _sttManagerMock;
    private readonly Mock<IServiceStatusUpdater> _statusUpdaterMock;

    public ServiceLifecycleManagerTests()
    {
        _loggerMock = new Mock<ILogger<ServiceLifecycleManager>>();
        _sttManagerMock = new Mock<ISpeechToTextServiceManager>();
        _statusUpdaterMock = new Mock<IServiceStatusUpdater>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new ServiceLifecycleManager(null!, _sttManagerMock.Object, _statusUpdaterMock.Object));
        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullSttManager_DoesNotThrow()
    {
        // Act & Assert - should not throw
        var service = new ServiceLifecycleManager(_loggerMock.Object, null, _statusUpdaterMock.Object);
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNullStatusUpdater_DoesNotThrow()
    {
        // Act & Assert - should not throw
        var service = new ServiceLifecycleManager(_loggerMock.Object, _sttManagerMock.Object, null);
        Assert.NotNull(service);
    }

    #endregion

    #region HandleStartSpeechToTextAsync Tests

    [Fact]
    public async Task HandleStartSpeechToTextAsync_WithNullSttManager_LogsWarning()
    {
        // Arrange
        var service = new ServiceLifecycleManager(_loggerMock.Object, null, _statusUpdaterMock.Object);

        // Act
        await service.HandleStartSpeechToTextAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SpeechToTextServiceManager not available")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleStartSpeechToTextAsync_WhenSuccess_RefreshesStatus()
    {
        // Arrange
        _sttManagerMock.Setup(x => x.StartAsync()).ReturnsAsync(true);
        _sttManagerMock.Setup(x => x.IsRunningAsync()).ReturnsAsync(true);
        _sttManagerMock.Setup(x => x.GetVersion()).Returns("1.0.0");

        var service = new ServiceLifecycleManager(_loggerMock.Object, _sttManagerMock.Object, _statusUpdaterMock.Object);

        // Act
        await service.HandleStartSpeechToTextAsync();

        // Assert
        _sttManagerMock.Verify(x => x.StartAsync(), Times.Once);
        _sttManagerMock.Verify(x => x.IsRunningAsync(), Times.Once);
        _statusUpdaterMock.Verify(x => x.UpdateSpeechToTextStatus(true, "1.0.0"), Times.Once);
    }

    [Fact]
    public async Task HandleStartSpeechToTextAsync_WhenFailure_DoesNotRefreshStatus()
    {
        // Arrange
        _sttManagerMock.Setup(x => x.StartAsync()).ReturnsAsync(false);

        var service = new ServiceLifecycleManager(_loggerMock.Object, _sttManagerMock.Object, _statusUpdaterMock.Object);

        // Act
        await service.HandleStartSpeechToTextAsync();

        // Assert
        _sttManagerMock.Verify(x => x.StartAsync(), Times.Once);
        _sttManagerMock.Verify(x => x.IsRunningAsync(), Times.Never);
        _statusUpdaterMock.Verify(x => x.UpdateSpeechToTextStatus(It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleStartSpeechToTextAsync_WhenException_LogsError()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Start failed");
        _sttManagerMock.Setup(x => x.StartAsync()).ThrowsAsync(expectedException);

        var service = new ServiceLifecycleManager(_loggerMock.Object, _sttManagerMock.Object, _statusUpdaterMock.Object);

        // Act
        await service.HandleStartSpeechToTextAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to start SpeechToText service")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region HandleStopSpeechToTextAsync Tests

    [Fact]
    public async Task HandleStopSpeechToTextAsync_WithNullSttManager_LogsWarning()
    {
        // Arrange
        var service = new ServiceLifecycleManager(_loggerMock.Object, null, _statusUpdaterMock.Object);

        // Act
        await service.HandleStopSpeechToTextAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SpeechToTextServiceManager not available")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleStopSpeechToTextAsync_WhenSuccess_RefreshesStatus()
    {
        // Arrange
        _sttManagerMock.Setup(x => x.StopAsync()).ReturnsAsync(true);
        _sttManagerMock.Setup(x => x.IsRunningAsync()).ReturnsAsync(false);
        _sttManagerMock.Setup(x => x.GetVersion()).Returns("1.0.0");

        var service = new ServiceLifecycleManager(_loggerMock.Object, _sttManagerMock.Object, _statusUpdaterMock.Object);

        // Act
        await service.HandleStopSpeechToTextAsync();

        // Assert
        _sttManagerMock.Verify(x => x.StopAsync(), Times.Once);
        _sttManagerMock.Verify(x => x.IsRunningAsync(), Times.Once);
        _statusUpdaterMock.Verify(x => x.UpdateSpeechToTextStatus(false, "1.0.0"), Times.Once);
    }

    [Fact]
    public async Task HandleStopSpeechToTextAsync_WhenException_LogsError()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Stop failed");
        _sttManagerMock.Setup(x => x.StopAsync()).ThrowsAsync(expectedException);

        var service = new ServiceLifecycleManager(_loggerMock.Object, _sttManagerMock.Object, _statusUpdaterMock.Object);

        // Act
        await service.HandleStopSpeechToTextAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to stop SpeechToText service")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region RefreshSpeechToTextStatusAsync Tests

    [Fact]
    public async Task RefreshSpeechToTextStatusAsync_WithNullSttManager_DoesNothing()
    {
        // Arrange
        var service = new ServiceLifecycleManager(_loggerMock.Object, null, _statusUpdaterMock.Object);

        // Act
        await service.RefreshSpeechToTextStatusAsync();

        // Assert
        _statusUpdaterMock.Verify(x => x.UpdateSpeechToTextStatus(It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RefreshSpeechToTextStatusAsync_WithNullMenuHandler_DoesNothing()
    {
        // Arrange
        _sttManagerMock.Setup(x => x.IsRunningAsync()).ReturnsAsync(true);
        var service = new ServiceLifecycleManager(_loggerMock.Object, _sttManagerMock.Object, null);

        // Act
        await service.RefreshSpeechToTextStatusAsync();

        // Assert
        _sttManagerMock.Verify(x => x.IsRunningAsync(), Times.Never);
    }

    [Fact]
    public async Task RefreshSpeechToTextStatusAsync_WhenRunning_UpdatesMenuWithRunningStatus()
    {
        // Arrange
        _sttManagerMock.Setup(x => x.IsRunningAsync()).ReturnsAsync(true);
        _sttManagerMock.Setup(x => x.GetVersion()).Returns("2.0.0");

        var service = new ServiceLifecycleManager(_loggerMock.Object, _sttManagerMock.Object, _statusUpdaterMock.Object);

        // Act
        await service.RefreshSpeechToTextStatusAsync();

        // Assert
        _statusUpdaterMock.Verify(x => x.UpdateSpeechToTextStatus(true, "2.0.0"), Times.Once);
    }

    [Fact]
    public async Task RefreshSpeechToTextStatusAsync_WhenNotRunning_UpdatesMenuWithStoppedStatus()
    {
        // Arrange
        _sttManagerMock.Setup(x => x.IsRunningAsync()).ReturnsAsync(false);
        _sttManagerMock.Setup(x => x.GetVersion()).Returns("2.0.0");

        var service = new ServiceLifecycleManager(_loggerMock.Object, _sttManagerMock.Object, _statusUpdaterMock.Object);

        // Act
        await service.RefreshSpeechToTextStatusAsync();

        // Assert
        _statusUpdaterMock.Verify(x => x.UpdateSpeechToTextStatus(false, "2.0.0"), Times.Once);
    }

    [Fact]
    public async Task RefreshSpeechToTextStatusAsync_WhenException_LogsError()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Refresh failed");
        _sttManagerMock.Setup(x => x.IsRunningAsync()).ThrowsAsync(expectedException);

        var service = new ServiceLifecycleManager(_loggerMock.Object, _sttManagerMock.Object, _statusUpdaterMock.Object);

        // Act
        await service.RefreshSpeechToTextStatusAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to refresh SpeechToText status")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region HandleStartLogViewerAsync Tests

    [Fact]
    public async Task HandleStartLogViewerAsync_LogsInformation()
    {
        // Arrange
        var service = new ServiceLifecycleManager(_loggerMock.Object, _sttManagerMock.Object, _statusUpdaterMock.Object);

        // Act
        await service.HandleStartLogViewerAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting log-viewer service")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region HandleStopLogViewerAsync Tests

    [Fact]
    public async Task HandleStopLogViewerAsync_LogsInformation()
    {
        // Arrange
        var service = new ServiceLifecycleManager(_loggerMock.Object, _sttManagerMock.Object, _statusUpdaterMock.Object);

        // Act
        await service.HandleStopLogViewerAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Stopping log-viewer service")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region RefreshLogViewerStatusAsync Tests

    [Fact]
    public async Task RefreshLogViewerStatusAsync_WithNullMenuHandler_DoesNothing()
    {
        // Arrange
        var service = new ServiceLifecycleManager(_loggerMock.Object, _sttManagerMock.Object, null);

        // Act
        await service.RefreshLogViewerStatusAsync();

        // Assert - should not throw and should return without calling menu handler
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    #endregion
}
