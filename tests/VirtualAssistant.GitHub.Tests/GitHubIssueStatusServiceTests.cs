using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using VirtualAssistant.GitHub.Configuration;
using VirtualAssistant.GitHub.Services;

namespace VirtualAssistant.GitHub.Tests;

public class GitHubIssueStatusServiceTests
{
    [Fact]
    public void GitHubIssueStatus_DefaultValues()
    {
        // Arrange & Act
        var status = new GitHubIssueStatus();

        // Assert
        Assert.Equal(0, status.Number);
        Assert.Equal("unknown", status.State);
        Assert.Equal(string.Empty, status.Title);
        Assert.False(status.Found);
        Assert.Null(status.Error);
    }

    [Fact]
    public void GitHubIssueStatus_CanSetValues()
    {
        // Arrange & Act
        var status = new GitHubIssueStatus
        {
            Number = 215,
            State = "open",
            Title = "Test Issue",
            Found = true
        };

        // Assert
        Assert.Equal(215, status.Number);
        Assert.Equal("open", status.State);
        Assert.Equal("Test Issue", status.Title);
        Assert.True(status.Found);
    }

    [Fact]
    public void GitHubIssueStatus_NotFound()
    {
        // Arrange & Act
        var status = new GitHubIssueStatus
        {
            Number = 999,
            State = "not_found",
            Found = false,
            Error = "Issue #999 not found"
        };

        // Assert
        Assert.False(status.Found);
        Assert.Equal("Issue #999 not found", status.Error);
    }

    [Fact]
    public void GitHubIssueStatus_Error()
    {
        // Arrange & Act
        var status = new GitHubIssueStatus
        {
            Number = 215,
            State = "error",
            Found = false,
            Error = "API rate limit exceeded"
        };

        // Assert
        Assert.False(status.Found);
        Assert.Equal("error", status.State);
        Assert.NotNull(status.Error);
    }
}
