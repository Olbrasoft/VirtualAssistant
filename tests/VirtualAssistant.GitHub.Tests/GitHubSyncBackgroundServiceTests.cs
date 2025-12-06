using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using VirtualAssistant.GitHub.Configuration;
using VirtualAssistant.GitHub.Services;

namespace VirtualAssistant.GitHub.Tests;

public class GitHubSyncBackgroundServiceTests
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<ILogger<GitHubSyncBackgroundService>> _loggerMock;

    public GitHubSyncBackgroundServiceTests()
    {
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _loggerMock = new Mock<ILogger<GitHubSyncBackgroundService>>();
    }

    private GitHubSyncBackgroundService CreateService(GitHubSettings? settings = null)
    {
        settings ??= new GitHubSettings
        {
            Token = "test-token",
            Owner = "TestOwner",
            EnableScheduledSync = true,
            SyncIntervalMinutes = 60
        };

        var options = Options.Create(settings);
        return new GitHubSyncBackgroundService(_scopeFactoryMock.Object, options, _loggerMock.Object);
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var service = CreateService();

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNullScopeFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(new GitHubSettings());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GitHubSyncBackgroundService(null!, options, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GitHubSyncBackgroundService(_scopeFactoryMock.Object, null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(new GitHubSettings());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GitHubSyncBackgroundService(_scopeFactoryMock.Object, options, null!));
    }

    [Fact]
    public void IsEnabled_WhenEnabled_ReturnsTrue()
    {
        // Arrange
        var settings = new GitHubSettings { EnableScheduledSync = true };
        var service = CreateService(settings);

        // Act & Assert
        Assert.True(service.IsEnabled);
    }

    [Fact]
    public void IsEnabled_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        var settings = new GitHubSettings { EnableScheduledSync = false };
        var service = CreateService(settings);

        // Act & Assert
        Assert.False(service.IsEnabled);
    }

    [Fact]
    public void SyncIntervalMinutes_ReturnsConfiguredValue()
    {
        // Arrange
        var settings = new GitHubSettings { SyncIntervalMinutes = 30 };
        var service = CreateService(settings);

        // Act & Assert
        Assert.Equal(30, service.SyncIntervalMinutes);
    }

    [Fact]
    public void GetStatus_InitialState_ReturnsCorrectStatus()
    {
        // Arrange
        var settings = new GitHubSettings
        {
            Owner = "TestOwner",
            EnableScheduledSync = true,
            SyncIntervalMinutes = 60
        };
        var service = CreateService(settings);

        // Act
        var status = service.GetStatus();

        // Assert
        Assert.True(status.IsEnabled);
        Assert.Equal("TestOwner", status.Owner);
        Assert.Equal(60, status.SyncIntervalMinutes);
        Assert.Null(status.LastSyncTime);
        Assert.False(status.LastSyncSuccess);
        Assert.Null(status.LastSyncError);
        Assert.Equal(0, status.ConsecutiveFailures);
        Assert.Null(status.NextSyncTime);
    }

    [Fact]
    public void LastSyncTime_InitialState_IsNull()
    {
        // Arrange
        var service = CreateService();

        // Assert
        Assert.Null(service.LastSyncTime);
    }

    [Fact]
    public void ConsecutiveFailures_InitialState_IsZero()
    {
        // Arrange
        var service = CreateService();

        // Assert
        Assert.Equal(0, service.ConsecutiveFailures);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_ReturnsImmediately()
    {
        // Arrange
        var settings = new GitHubSettings { EnableScheduledSync = false };
        var service = CreateService(settings);
        using var cts = new CancellationTokenSource();

        // Act - start and immediately cancel
        var task = service.StartAsync(cts.Token);
        await Task.Delay(100); // Give it a moment
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        // Assert - no sync was attempted (no scope created)
        _scopeFactoryMock.Verify(x => x.CreateScope(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOwnerEmpty_ReturnsImmediately()
    {
        // Arrange
        var settings = new GitHubSettings
        {
            EnableScheduledSync = true,
            Owner = "" // Empty owner
        };
        var service = CreateService(settings);
        using var cts = new CancellationTokenSource();

        // Act
        var task = service.StartAsync(cts.Token);
        await Task.Delay(100);
        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        // Assert - no sync was attempted
        _scopeFactoryMock.Verify(x => x.CreateScope(), Times.Never);
    }
}
