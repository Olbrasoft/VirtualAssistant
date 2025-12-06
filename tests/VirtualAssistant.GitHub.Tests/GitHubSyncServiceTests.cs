using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using VirtualAssistant.Data.Entities;
using VirtualAssistant.Data.EntityFrameworkCore;
using VirtualAssistant.GitHub.Configuration;
using VirtualAssistant.GitHub.Services;

namespace VirtualAssistant.GitHub.Tests;

public class GitHubSyncServiceTests
{
    private readonly DbContextOptions<VirtualAssistantDbContext> _dbOptions;
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;

    public GitHubSyncServiceTests()
    {
        _dbOptions = new DbContextOptionsBuilder<VirtualAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _embeddingServiceMock.Setup(x => x.IsConfigured).Returns(true);
    }

    [Fact]
    public void Constructor_WithoutToken_LogsWarning()
    {
        // Arrange
        using var dbContext = new VirtualAssistantDbContext(_dbOptions);
        var settings = Options.Create(new GitHubSettings { Token = "", Owner = "Olbrasoft" });
        var logger = new Mock<ILogger<GitHubSyncService>>();

        // Act
        var service = new GitHubSyncService(dbContext, settings, _embeddingServiceMock.Object, logger.Object);

        // Assert
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("without authentication")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithToken_LogsInformation()
    {
        // Arrange
        using var dbContext = new VirtualAssistantDbContext(_dbOptions);
        var settings = Options.Create(new GitHubSettings { Token = "ghp_test", Owner = "Olbrasoft" });
        var logger = new Mock<ILogger<GitHubSyncService>>();

        // Act
        var service = new GitHubSyncService(dbContext, settings, _embeddingServiceMock.Object, logger.Object);

        // Assert
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("authentication token")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SyncIssuesAsync_WithNonExistentRepository_ReturnsZero()
    {
        // Arrange
        using var dbContext = new VirtualAssistantDbContext(_dbOptions);
        var settings = Options.Create(new GitHubSettings { Token = "", Owner = "Olbrasoft" });
        var logger = new Mock<ILogger<GitHubSyncService>>();
        var service = new GitHubSyncService(dbContext, settings, _embeddingServiceMock.Object, logger.Object);

        // Act
        var result = await service.SyncIssuesAsync(999);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GenerateMissingEmbeddingsAsync_WhenNotConfigured_ReturnsZero()
    {
        // Arrange
        using var dbContext = new VirtualAssistantDbContext(_dbOptions);
        var settings = Options.Create(new GitHubSettings { Token = "", Owner = "Olbrasoft" });
        var logger = new Mock<ILogger<GitHubSyncService>>();
        var embeddingServiceMock = new Mock<IEmbeddingService>();
        embeddingServiceMock.Setup(x => x.IsConfigured).Returns(false);

        var service = new GitHubSyncService(dbContext, settings, embeddingServiceMock.Object, logger.Object);

        // Act
        var result = await service.GenerateMissingEmbeddingsAsync();

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GenerateMissingEmbeddingsAsync_WithNoIssues_ReturnsZero()
    {
        // Arrange
        using var dbContext = new VirtualAssistantDbContext(_dbOptions);
        var settings = Options.Create(new GitHubSettings { Token = "", Owner = "Olbrasoft" });
        var logger = new Mock<ILogger<GitHubSyncService>>();
        var service = new GitHubSyncService(dbContext, settings, _embeddingServiceMock.Object, logger.Object);

        // Act
        var result = await service.GenerateMissingEmbeddingsAsync();

        // Assert
        Assert.Equal(0, result);
    }
}
