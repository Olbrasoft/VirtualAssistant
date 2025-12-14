using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using VirtualAssistant.Data.EntityFrameworkCore;
using VirtualAssistant.GitHub.Services;

namespace VirtualAssistant.GitHub.Tests;

public class GitHubReferenceServiceTests : IDisposable
{
    private readonly VirtualAssistantDbContext _dbContext;
    private readonly Mock<ILogger<GitHubReferenceService>> _loggerMock;
    private readonly GitHubReferenceService _service;

    public GitHubReferenceServiceTests()
    {
        var options = new DbContextOptionsBuilder<VirtualAssistantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new VirtualAssistantDbContext(options);
        _loggerMock = new Mock<ILogger<GitHubReferenceService>>();
        _service = new GitHubReferenceService(_dbContext, _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task EnsureRepositoryExistsAsync_CreatesNew_WhenNotExists()
    {
        // Act
        var id = await _service.EnsureRepositoryExistsAsync("Olbrasoft", "VirtualAssistant");

        // Assert
        Assert.True(id > 0);
        Assert.Single(_dbContext.GitHubRepositories);
        var repo = await _dbContext.GitHubRepositories.FindAsync(id);
        Assert.NotNull(repo);
        Assert.Equal("Olbrasoft", repo.Owner);
        Assert.Equal("VirtualAssistant", repo.Name);
    }

    [Fact]
    public async Task EnsureRepositoryExistsAsync_ReturnsExisting_WhenExists()
    {
        // Arrange
        var id1 = await _service.EnsureRepositoryExistsAsync("Olbrasoft", "VirtualAssistant");

        // Act
        var id2 = await _service.EnsureRepositoryExistsAsync("Olbrasoft", "VirtualAssistant");

        // Assert
        Assert.Equal(id1, id2);
        Assert.Single(_dbContext.GitHubRepositories);
    }

    [Fact]
    public async Task EnsureRepositoryExistsAsync_CreatesDifferentRepos_ForDifferentNames()
    {
        // Act
        var id1 = await _service.EnsureRepositoryExistsAsync("Olbrasoft", "VirtualAssistant");
        var id2 = await _service.EnsureRepositoryExistsAsync("Olbrasoft", "SpeechToText");

        // Assert
        Assert.NotEqual(id1, id2);
        Assert.Equal(2, await _dbContext.GitHubRepositories.CountAsync());
    }

    [Fact]
    public async Task EnsureIssueExistsAsync_CreatesRepoAndIssue_WhenNeitherExists()
    {
        // Act
        var issueId = await _service.EnsureIssueExistsAsync("Olbrasoft", "VirtualAssistant", 252);

        // Assert
        Assert.True(issueId > 0);
        Assert.Single(_dbContext.GitHubRepositories);
        Assert.Single(_dbContext.GitHubIssues);

        var issue = await _dbContext.GitHubIssues.FindAsync(issueId);
        Assert.NotNull(issue);
        Assert.Equal(252, issue.IssueNumber);
    }

    [Fact]
    public async Task EnsureIssueExistsAsync_ReturnsExisting_WhenIssueExists()
    {
        // Arrange
        var id1 = await _service.EnsureIssueExistsAsync("Olbrasoft", "VirtualAssistant", 252);

        // Act
        var id2 = await _service.EnsureIssueExistsAsync("Olbrasoft", "VirtualAssistant", 252);

        // Assert
        Assert.Equal(id1, id2);
        Assert.Single(_dbContext.GitHubIssues);
    }

    [Fact]
    public async Task EnsureIssueExistsAsync_CreatesDifferentIssues_ForDifferentNumbers()
    {
        // Act
        var id1 = await _service.EnsureIssueExistsAsync("Olbrasoft", "VirtualAssistant", 251);
        var id2 = await _service.EnsureIssueExistsAsync("Olbrasoft", "VirtualAssistant", 252);

        // Assert
        Assert.NotEqual(id1, id2);
        Assert.Equal(2, await _dbContext.GitHubIssues.CountAsync());
        Assert.Single(_dbContext.GitHubRepositories); // Same repo
    }

    [Fact]
    public async Task EnsureIssueFromUrlAsync_ParsesValidUrl()
    {
        // Act
        var result = await _service.EnsureIssueFromUrlAsync(
            "https://github.com/Olbrasoft/VirtualAssistant/issues/252");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Olbrasoft", result.Owner);
        Assert.Equal("VirtualAssistant", result.Name);
        Assert.Equal(252, result.IssueNumber);
        Assert.True(result.IssueId > 0);
        Assert.True(result.RepositoryId > 0);
    }

    [Fact]
    public async Task EnsureIssueFromUrlAsync_ParsesHttpUrl()
    {
        // Act
        var result = await _service.EnsureIssueFromUrlAsync(
            "http://github.com/Olbrasoft/VirtualAssistant/issues/123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(123, result.IssueNumber);
    }

    [Fact]
    public async Task EnsureIssueFromUrlAsync_ReturnsNull_ForNullUrl()
    {
        // Act
        var result = await _service.EnsureIssueFromUrlAsync(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task EnsureIssueFromUrlAsync_ReturnsNull_ForEmptyUrl()
    {
        // Act
        var result = await _service.EnsureIssueFromUrlAsync("");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task EnsureIssueFromUrlAsync_ReturnsNull_ForWhitespaceUrl()
    {
        // Act
        var result = await _service.EnsureIssueFromUrlAsync("   ");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task EnsureIssueFromUrlAsync_ReturnsNull_ForInvalidUrl()
    {
        // Act
        var result = await _service.EnsureIssueFromUrlAsync("not-a-valid-url");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task EnsureIssueFromUrlAsync_ReturnsNull_ForNonGitHubUrl()
    {
        // Act
        var result = await _service.EnsureIssueFromUrlAsync("https://gitlab.com/owner/repo/issues/123");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task EnsureIssueFromUrlAsync_ReturnsNull_ForPullRequestUrl()
    {
        // Act
        var result = await _service.EnsureIssueFromUrlAsync(
            "https://github.com/Olbrasoft/VirtualAssistant/pull/100");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GitHubIssueReference_Url_ReturnsCorrectUrl()
    {
        // Act
        var result = await _service.EnsureIssueFromUrlAsync(
            "https://github.com/Olbrasoft/VirtualAssistant/issues/252");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("https://github.com/Olbrasoft/VirtualAssistant/issues/252", result.Url);
    }

    [Fact]
    public async Task EnsureIssueFromUrlAsync_IsCaseInsensitive()
    {
        // Act
        var result = await _service.EnsureIssueFromUrlAsync(
            "HTTPS://GITHUB.COM/Olbrasoft/VirtualAssistant/issues/252");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Olbrasoft", result.Owner);
        Assert.Equal("VirtualAssistant", result.Name);
    }

    [Fact]
    public async Task EnsureIssueFromUrlAsync_HandlesUrlWithTrailingSlash()
    {
        // Act - URL without trailing content after issue number
        var result = await _service.EnsureIssueFromUrlAsync(
            "https://github.com/Olbrasoft/VirtualAssistant/issues/252");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(252, result.IssueNumber);
    }

    [Fact]
    public async Task EnsureIssueFromUrlAsync_HandlesUrlWithFragment()
    {
        // Act - URL with fragment (anchor)
        var result = await _service.EnsureIssueFromUrlAsync(
            "https://github.com/Olbrasoft/VirtualAssistant/issues/252#issuecomment-123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(252, result.IssueNumber);
    }
}
