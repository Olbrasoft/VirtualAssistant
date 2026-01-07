using Microsoft.Extensions.Logging;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Data.Commands.GitHubCommands;
using Olbrasoft.VirtualAssistant.GitHub.Services;

namespace Olbrasoft.VirtualAssistant.GitHub.Tests;

/// <summary>
/// Unit tests for GitHubReferenceService using mocked CQRS infrastructure.
/// </summary>
public class GitHubReferenceServiceTests
{
    private readonly Mock<ICommandExecutor> _mockCommandExecutor;
    private readonly Mock<ILogger<GitHubReferenceService>> _mockLogger;
    private readonly GitHubReferenceService _service;

    public GitHubReferenceServiceTests()
    {
        _mockCommandExecutor = new Mock<ICommandExecutor>();
        _mockLogger = new Mock<ILogger<GitHubReferenceService>>();
        _service = new GitHubReferenceService(_mockCommandExecutor.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task EnsureRepositoryExistsAsync_ExecutesCommand_ReturnsRepoId()
    {
        // Arrange
        const int expectedId = 42;
        _mockCommandExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<EnsureRepositoryExistsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedId);

        // Act
        var result = await _service.EnsureRepositoryExistsAsync("Olbrasoft", "VirtualAssistant");

        // Assert
        Assert.Equal(expectedId, result);
        _mockCommandExecutor.Verify(x => x.ExecuteAsync(
            It.Is<EnsureRepositoryExistsCommand>(c => c.Owner == "Olbrasoft" && c.Name == "VirtualAssistant"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureIssueExistsAsync_ExecutesCommand_ReturnsIssueId()
    {
        // Arrange
        const int expectedId = 100;
        _mockCommandExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<EnsureIssueExistsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedId);

        // Act
        var result = await _service.EnsureIssueExistsAsync("Olbrasoft", "VirtualAssistant", 252);

        // Assert
        Assert.Equal(expectedId, result);
        _mockCommandExecutor.Verify(x => x.ExecuteAsync(
            It.Is<EnsureIssueExistsCommand>(c =>
                c.Owner == "Olbrasoft" &&
                c.Name == "VirtualAssistant" &&
                c.IssueNumber == 252),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureIssueFromUrlAsync_ParsesValidUrl_ReturnsReference()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<EnsureRepositoryExistsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _mockCommandExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<EnsureIssueExistsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        // Act
        var result = await _service.EnsureIssueFromUrlAsync(
            "https://github.com/Olbrasoft/VirtualAssistant/issues/252");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Olbrasoft", result.Owner);
        Assert.Equal("VirtualAssistant", result.Name);
        Assert.Equal(252, result.IssueNumber);
        Assert.Equal(100, result.IssueId);
        Assert.Equal(1, result.RepositoryId);
    }

    [Fact]
    public async Task EnsureIssueFromUrlAsync_ParsesHttpUrl()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<EnsureRepositoryExistsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _mockCommandExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<EnsureIssueExistsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

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
        _mockCommandExecutor.Verify(x => x.ExecuteAsync(
            It.IsAny<EnsureRepositoryExistsCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
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
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<EnsureRepositoryExistsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _mockCommandExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<EnsureIssueExistsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

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
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<EnsureRepositoryExistsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _mockCommandExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<EnsureIssueExistsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        // Act
        var result = await _service.EnsureIssueFromUrlAsync(
            "HTTPS://GITHUB.COM/Olbrasoft/VirtualAssistant/issues/252");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Olbrasoft", result.Owner);
        Assert.Equal("VirtualAssistant", result.Name);
    }

    [Fact]
    public async Task EnsureIssueFromUrlAsync_HandlesUrlWithFragment()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<EnsureRepositoryExistsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _mockCommandExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<EnsureIssueExistsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        // Act - URL with fragment (anchor)
        var result = await _service.EnsureIssueFromUrlAsync(
            "https://github.com/Olbrasoft/VirtualAssistant/issues/252#issuecomment-123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(252, result.IssueNumber);
    }
}
