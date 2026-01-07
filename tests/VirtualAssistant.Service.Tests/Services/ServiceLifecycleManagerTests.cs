using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Configuration;
using Olbrasoft.VirtualAssistant.Service.Infrastructure;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ServiceLifecycleManager"/>.
/// Verifies systemd service lifecycle management (start/stop/status).
/// NOTE: STT service tests removed (issue #466) - STT runs inline now.
/// </summary>
public class ServiceLifecycleManagerTests
{
    private readonly Mock<ILogger<ServiceLifecycleManager>> _loggerMock;
    private readonly Mock<IServiceStatusUpdater> _statusUpdaterMock;
    private readonly Mock<ISystemdServiceController> _serviceControllerMock;
    private readonly IOptions<ServiceMonitoringOptions> _defaultOptions;

    public ServiceLifecycleManagerTests()
    {
        _loggerMock = new Mock<ILogger<ServiceLifecycleManager>>();
        _statusUpdaterMock = new Mock<IServiceStatusUpdater>();
        _serviceControllerMock = new Mock<ISystemdServiceController>();
        _defaultOptions = Options.Create(new ServiceMonitoringOptions
        {
            StatusPollTimeoutMs = 2000,
            StatusPollIntervalMs = 100
        });
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new ServiceLifecycleManager(null!, _defaultOptions, _serviceControllerMock.Object, _statusUpdaterMock.Object));
        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullServiceController_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new ServiceLifecycleManager(_loggerMock.Object, _defaultOptions, null!, _statusUpdaterMock.Object));
        Assert.Equal("serviceController", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullStatusUpdater_DoesNotThrow()
    {
        // Act & Assert - should not throw
        var service = new ServiceLifecycleManager(_loggerMock.Object, _defaultOptions, _serviceControllerMock.Object, null);
        Assert.NotNull(service);
    }

    #endregion

    // NOTE: STT service tests removed (issue #466) - STT runs inline now

    #region HandleStartLogViewerAsync Tests

    [Fact]
    public async Task HandleStartLogViewerAsync_LogsInformation()
    {
        // Arrange
        _serviceControllerMock.Setup(x => x.StartServiceAsync(It.IsAny<string>())).ReturnsAsync(false);
        var service = new ServiceLifecycleManager(_loggerMock.Object, _defaultOptions, _serviceControllerMock.Object, _statusUpdaterMock.Object);

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

    [Fact]
    public async Task HandleStartLogViewerAsync_CallsServiceController()
    {
        // Arrange
        _serviceControllerMock.Setup(x => x.StartServiceAsync(It.IsAny<string>())).ReturnsAsync(true);
        _serviceControllerMock.Setup(x => x.IsRunningAsync(It.IsAny<string>())).ReturnsAsync(true);
        var service = new ServiceLifecycleManager(_loggerMock.Object, _defaultOptions, _serviceControllerMock.Object, _statusUpdaterMock.Object);

        // Act
        await service.HandleStartLogViewerAsync();

        // Assert
        _serviceControllerMock.Verify(x => x.StartServiceAsync("log-viewer.service"), Times.Once);
    }

    #endregion

    #region HandleStopLogViewerAsync Tests

    [Fact]
    public async Task HandleStopLogViewerAsync_LogsInformation()
    {
        // Arrange
        _serviceControllerMock.Setup(x => x.StopServiceAsync(It.IsAny<string>())).ReturnsAsync(false);
        var service = new ServiceLifecycleManager(_loggerMock.Object, _defaultOptions, _serviceControllerMock.Object, _statusUpdaterMock.Object);

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

    [Fact]
    public async Task HandleStopLogViewerAsync_CallsServiceController()
    {
        // Arrange
        _serviceControllerMock.Setup(x => x.StopServiceAsync(It.IsAny<string>())).ReturnsAsync(true);
        _serviceControllerMock.Setup(x => x.IsRunningAsync(It.IsAny<string>())).ReturnsAsync(false);
        var service = new ServiceLifecycleManager(_loggerMock.Object, _defaultOptions, _serviceControllerMock.Object, _statusUpdaterMock.Object);

        // Act
        await service.HandleStopLogViewerAsync();

        // Assert
        _serviceControllerMock.Verify(x => x.StopServiceAsync("log-viewer.service"), Times.Once);
    }

    #endregion

    #region RefreshLogViewerStatusAsync Tests

    [Fact]
    public async Task RefreshLogViewerStatusAsync_WithNullMenuHandler_DoesNothing()
    {
        // Arrange
        var service = new ServiceLifecycleManager(_loggerMock.Object, _defaultOptions, _serviceControllerMock.Object, null);

        // Act
        await service.RefreshLogViewerStatusAsync();

        // Assert - should not throw and should not call service controller
        _serviceControllerMock.Verify(x => x.IsRunningAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RefreshLogViewerStatusAsync_WhenServiceRunning_UpdatesStatusToTrue()
    {
        // Arrange
        _serviceControllerMock.Setup(x => x.IsRunningAsync("log-viewer.service")).ReturnsAsync(true);
        var service = new ServiceLifecycleManager(_loggerMock.Object, _defaultOptions, _serviceControllerMock.Object, _statusUpdaterMock.Object);

        // Act
        await service.RefreshLogViewerStatusAsync();

        // Assert
        _statusUpdaterMock.Verify(x => x.UpdateLogViewerStatus(true), Times.Once);
    }

    [Fact]
    public async Task RefreshLogViewerStatusAsync_WhenServiceStopped_UpdatesStatusToFalse()
    {
        // Arrange
        _serviceControllerMock.Setup(x => x.IsRunningAsync("log-viewer.service")).ReturnsAsync(false);
        var service = new ServiceLifecycleManager(_loggerMock.Object, _defaultOptions, _serviceControllerMock.Object, _statusUpdaterMock.Object);

        // Act
        await service.RefreshLogViewerStatusAsync();

        // Assert
        _statusUpdaterMock.Verify(x => x.UpdateLogViewerStatus(false), Times.Once);
    }

    #endregion
}
