using VirtualAssistant.Data.Entities;
using VirtualAssistant.GitHub.Dtos;

namespace VirtualAssistant.GitHub.Tests;

public class GitHubIssueDtoTests
{
    [Fact]
    public void FromEntity_MapsAllProperties()
    {
        // Arrange
        var repository = new GitHubRepository
        {
            Id = 1,
            Owner = "Olbrasoft",
            Name = "VirtualAssistant",
            FullName = "Olbrasoft/VirtualAssistant",
            HtmlUrl = "https://github.com/Olbrasoft/VirtualAssistant",
            SyncedAt = DateTime.UtcNow
        };

        var issue = new GitHubIssue
        {
            Id = 42,
            IssueNumber = 150,
            Title = "Test Issue",
            Body = "This is the body",
            State = "open",
            HtmlUrl = "https://github.com/Olbrasoft/VirtualAssistant/issues/150",
            CreatedAt = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2025, 1, 16, 12, 0, 0, DateTimeKind.Utc),
            SyncedAt = DateTime.UtcNow,
            Repository = repository,
            Agents = new List<GitHubIssueAgent>
            {
                new() { Agent = "claude" },
                new() { Agent = "opencode" }
            }
        };

        // Act
        var dto = GitHubIssueDto.FromEntity(issue, 0.95f);

        // Assert
        Assert.Equal(42, dto.Id);
        Assert.Equal(150, dto.IssueNumber);
        Assert.Equal("Test Issue", dto.Title);
        Assert.Equal("This is the body", dto.Body);
        Assert.Equal("open", dto.State);
        Assert.Equal("https://github.com/Olbrasoft/VirtualAssistant/issues/150", dto.HtmlUrl);
        Assert.Equal("Olbrasoft/VirtualAssistant", dto.RepositoryFullName);
        Assert.Equal(0.95f, dto.Similarity);
        Assert.Equal(2, dto.Agents.Count);
        Assert.Contains("claude", dto.Agents);
        Assert.Contains("opencode", dto.Agents);
        Assert.Equal(new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc), dto.CreatedAt);
        Assert.Equal(new DateTime(2025, 1, 16, 12, 0, 0, DateTimeKind.Utc), dto.UpdatedAt);
    }

    [Fact]
    public void FromEntity_WithNullSimilarity_SetsSimilarityToNull()
    {
        // Arrange
        var issue = new GitHubIssue
        {
            Id = 1,
            IssueNumber = 1,
            Title = "Test",
            State = "open",
            HtmlUrl = "https://example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow,
            Repository = new GitHubRepository { FullName = "test/repo" }
        };

        // Act
        var dto = GitHubIssueDto.FromEntity(issue);

        // Assert
        Assert.Null(dto.Similarity);
    }

    [Fact]
    public void FromEntity_TruncatesLongBody()
    {
        // Arrange
        var longBody = new string('x', 600); // 600 characters, max is 500
        var issue = new GitHubIssue
        {
            Id = 1,
            IssueNumber = 1,
            Title = "Test",
            Body = longBody,
            State = "open",
            HtmlUrl = "https://example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow,
            Repository = new GitHubRepository { FullName = "test/repo" }
        };

        // Act
        var dto = GitHubIssueDto.FromEntity(issue);

        // Assert
        Assert.NotNull(dto.Body);
        Assert.Equal(503, dto.Body.Length); // 500 + "..."
        Assert.EndsWith("...", dto.Body);
    }

    [Fact]
    public void FromEntity_DoesNotTruncateShortBody()
    {
        // Arrange
        var shortBody = "Short body text";
        var issue = new GitHubIssue
        {
            Id = 1,
            IssueNumber = 1,
            Title = "Test",
            Body = shortBody,
            State = "open",
            HtmlUrl = "https://example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow,
            Repository = new GitHubRepository { FullName = "test/repo" }
        };

        // Act
        var dto = GitHubIssueDto.FromEntity(issue);

        // Assert
        Assert.Equal(shortBody, dto.Body);
    }

    [Fact]
    public void FromEntity_HandlesNullBody()
    {
        // Arrange
        var issue = new GitHubIssue
        {
            Id = 1,
            IssueNumber = 1,
            Title = "Test",
            Body = null,
            State = "open",
            HtmlUrl = "https://example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow,
            Repository = new GitHubRepository { FullName = "test/repo" }
        };

        // Act
        var dto = GitHubIssueDto.FromEntity(issue);

        // Assert
        Assert.Null(dto.Body);
    }

    [Fact]
    public void FromEntity_HandlesNullRepository()
    {
        // Arrange
        var issue = new GitHubIssue
        {
            Id = 1,
            IssueNumber = 1,
            Title = "Test",
            State = "open",
            HtmlUrl = "https://example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow,
            Repository = null!
        };

        // Act
        var dto = GitHubIssueDto.FromEntity(issue);

        // Assert
        Assert.Equal(string.Empty, dto.RepositoryFullName);
    }

    [Fact]
    public void FromEntity_HandlesNullAgents()
    {
        // Arrange
        var issue = new GitHubIssue
        {
            Id = 1,
            IssueNumber = 1,
            Title = "Test",
            State = "open",
            HtmlUrl = "https://example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow,
            Repository = new GitHubRepository { FullName = "test/repo" },
            Agents = null!
        };

        // Act
        var dto = GitHubIssueDto.FromEntity(issue);

        // Assert
        Assert.Empty(dto.Agents);
    }
}
