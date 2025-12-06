using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Pgvector;
using VirtualAssistant.Data.Entities;
using VirtualAssistant.Data.EntityFrameworkCore;
using VirtualAssistant.GitHub.Services;

namespace VirtualAssistant.GitHub.Tests;

public class GitHubSearchServiceTests
{
    private readonly DbContextOptions<VirtualAssistantDbContext> _dbOptions;
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<ILogger<GitHubSearchService>> _loggerMock;

    public GitHubSearchServiceTests()
    {
        _dbOptions = new DbContextOptionsBuilder<VirtualAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _loggerMock = new Mock<ILogger<GitHubSearchService>>();
    }

    private GitHubSearchService CreateService(VirtualAssistantDbContext dbContext)
    {
        return new GitHubSearchService(dbContext, _embeddingServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public void IsConfigured_ReturnsEmbeddingServiceStatus()
    {
        // Arrange
        using var dbContext = new VirtualAssistantDbContext(_dbOptions);
        _embeddingServiceMock.Setup(x => x.IsConfigured).Returns(true);
        var service = CreateService(dbContext);

        // Act & Assert
        Assert.True(service.IsConfigured);

        _embeddingServiceMock.Setup(x => x.IsConfigured).Returns(false);
        var service2 = CreateService(dbContext);
        Assert.False(service2.IsConfigured);
    }

    [Fact]
    public async Task SearchSimilarAsync_WithEmptyQuery_ReturnsEmpty()
    {
        // Arrange
        using var dbContext = new VirtualAssistantDbContext(_dbOptions);
        _embeddingServiceMock.Setup(x => x.IsConfigured).Returns(true);
        var service = CreateService(dbContext);

        // Act
        var result = await service.SearchSimilarAsync("");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchSimilarAsync_WhenNotConfigured_ReturnsEmpty()
    {
        // Arrange
        using var dbContext = new VirtualAssistantDbContext(_dbOptions);
        _embeddingServiceMock.Setup(x => x.IsConfigured).Returns(false);
        var service = CreateService(dbContext);

        // Act
        var result = await service.SearchSimilarAsync("test query");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchSimilarAsync_WhenEmbeddingGenerationFails_ReturnsEmpty()
    {
        // Arrange
        using var dbContext = new VirtualAssistantDbContext(_dbOptions);
        _embeddingServiceMock.Setup(x => x.IsConfigured).Returns(true);
        _embeddingServiceMock.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Vector?)null);
        var service = CreateService(dbContext);

        // Act
        var result = await service.SearchSimilarAsync("test query");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetOpenIssuesAsync_WithEmptyRepoName_ReturnsEmpty()
    {
        // Arrange
        using var dbContext = new VirtualAssistantDbContext(_dbOptions);
        _embeddingServiceMock.Setup(x => x.IsConfigured).Returns(true);
        var service = CreateService(dbContext);

        // Act
        var result = await service.GetOpenIssuesAsync("");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetOpenIssuesAsync_WithNoMatchingRepo_ReturnsEmpty()
    {
        // Arrange
        using var dbContext = new VirtualAssistantDbContext(_dbOptions);
        _embeddingServiceMock.Setup(x => x.IsConfigured).Returns(true);
        var service = CreateService(dbContext);

        // Act
        var result = await service.GetOpenIssuesAsync("NonExistent/Repo");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetOpenIssuesAsync_ReturnsOnlyOpenIssues()
    {
        // Arrange
        using var dbContext = new VirtualAssistantDbContext(_dbOptions);

        var repo = new GitHubRepository
        {
            Id = 1,
            Owner = "Olbrasoft",
            Name = "VirtualAssistant",
            FullName = "Olbrasoft/VirtualAssistant",
            HtmlUrl = "https://github.com/Olbrasoft/VirtualAssistant",
            SyncedAt = DateTime.UtcNow
        };
        dbContext.GitHubRepositories.Add(repo);

        dbContext.GitHubIssues.Add(new GitHubIssue
        {
            Id = 1,
            RepositoryId = 1,
            IssueNumber = 1,
            Title = "Open Issue",
            State = "open",
            HtmlUrl = "https://github.com/Olbrasoft/VirtualAssistant/issues/1",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow
        });

        dbContext.GitHubIssues.Add(new GitHubIssue
        {
            Id = 2,
            RepositoryId = 1,
            IssueNumber = 2,
            Title = "Closed Issue",
            State = "closed",
            HtmlUrl = "https://github.com/Olbrasoft/VirtualAssistant/issues/2",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();

        _embeddingServiceMock.Setup(x => x.IsConfigured).Returns(true);
        var service = CreateService(dbContext);

        // Act
        var result = await service.GetOpenIssuesAsync("Olbrasoft/VirtualAssistant");

        // Assert
        Assert.Single(result);
        Assert.Equal("Open Issue", result[0].Title);
        Assert.Equal("open", result[0].State);
    }

    [Fact]
    public async Task FindDuplicatesAsync_WithEmptyTitle_ReturnsEmpty()
    {
        // Arrange
        using var dbContext = new VirtualAssistantDbContext(_dbOptions);
        _embeddingServiceMock.Setup(x => x.IsConfigured).Returns(true);
        var service = CreateService(dbContext);

        // Act
        var result = await service.FindDuplicatesAsync("");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task FindDuplicatesAsync_WhenNotConfigured_ReturnsEmpty()
    {
        // Arrange
        using var dbContext = new VirtualAssistantDbContext(_dbOptions);
        _embeddingServiceMock.Setup(x => x.IsConfigured).Returns(false);
        var service = CreateService(dbContext);

        // Act
        var result = await service.FindDuplicatesAsync("Test Title");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task FindDuplicatesAsync_WhenEmbeddingGenerationFails_ReturnsEmpty()
    {
        // Arrange
        using var dbContext = new VirtualAssistantDbContext(_dbOptions);
        _embeddingServiceMock.Setup(x => x.IsConfigured).Returns(true);
        _embeddingServiceMock.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Vector?)null);
        var service = CreateService(dbContext);

        // Act
        var result = await service.FindDuplicatesAsync("Test Title");

        // Assert
        Assert.Empty(result);
    }
}
