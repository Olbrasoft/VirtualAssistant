using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Configuration;
using Olbrasoft.VirtualAssistant.Service.Services;

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
    private readonly IOptions<ServiceMonitoringOptions> _defaultOptions;

    public ServiceLifecycleManagerTests()
    {
        _loggerMock = new Mock<ILogger<ServiceLifecycleManager>>();
        _statusUpdaterMock = new Mock<IServiceStatusUpdater>();
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
            new ServiceLifecycleManager(null!, _defaultOptions, _statusUpdaterMock.Object));
        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullStatusUpdater_DoesNotThrow()
    {
        // Act & Assert - should not throw
        var service = new ServiceLifecycleManager(_loggerMock.Object, _defaultOptions, null);
        Assert.NotNull(service);
    }

    #endregion

    // NOTE: STT service tests removed (issue #466) - STT runs inline now

    #region HandleStartLogViewerAsync Tests

    [Fact]
    public async Task HandleStartLogViewerAsync_LogsInformation()
    {
        // Arrange
        var service = new ServiceLifecycleManager(_loggerMock.Object, _defaultOptions, _statusUpdaterMock.Object);

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
        var service = new ServiceLifecycleManager(_loggerMock.Object, _defaultOptions, _statusUpdaterMock.Object);

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

    // TODO: Add integration tests or use IProcessWrapper for testing success paths
    // - RefreshLogViewerStatusAsync_WhenServiceRunning_UpdatesStatusToTrue
    // - RefreshLogViewerStatusAsync_WhenServiceStopped_UpdatesStatusToFalse
    // Current implementation calls Process.Start which has system side-effects
    // Requires IProcessWrapper abstraction or integration test infrastructure

    [Fact]
    public async Task RefreshLogViewerStatusAsync_WithNullMenuHandler_DoesNothing()
    {
        // Arrange
        var service = new ServiceLifecycleManager(_loggerMock.Object, _defaultOptions, null);

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
