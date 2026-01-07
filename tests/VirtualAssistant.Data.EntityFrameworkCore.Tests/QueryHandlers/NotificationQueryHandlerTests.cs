using Microsoft.EntityFrameworkCore;
using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Data.Enums;
using Olbrasoft.VirtualAssistant.Data.Queries.NotificationQueries;
using Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.NotificationQueryHandlers;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Tests.QueryHandlers;

/// <summary>
/// Unit tests for Notification query handlers using in-memory database.
/// </summary>
public class NotificationQueryHandlerTests
{
    private static VirtualAssistantDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<VirtualAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new VirtualAssistantDbContext(options);
        SeedRequiredData(context);
        return context;
    }

    private static void SeedRequiredData(VirtualAssistantDbContext context)
    {
        context.NotificationStatuses.AddRange(
            new NotificationStatus { Id = (int)NotificationStatusEnum.NewlyReceived, Name = "NewlyReceived" },
            new NotificationStatus { Id = (int)NotificationStatusEnum.Processing, Name = "Processing" },
            new NotificationStatus { Id = (int)NotificationStatusEnum.Played, Name = "Played" }
        );

        context.Agents.Add(new Agent { Id = (int)AgentType.ClaudeCode, Name = "Claude Code" });
        context.SaveChanges();
    }

    #region GetNewNotificationsQueryHandler Tests

    [Fact]
    public async Task GetNewNotifications_ReturnsOnlyNewlyReceivedNotifications()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        context.Notifications.AddRange(
            new Notification { Text = "New1", AgentId = (int)AgentType.ClaudeCode, NotificationStatusId = (int)NotificationStatusEnum.NewlyReceived, CreatedAt = DateTime.UtcNow },
            new Notification { Text = "New2", AgentId = (int)AgentType.ClaudeCode, NotificationStatusId = (int)NotificationStatusEnum.NewlyReceived, CreatedAt = DateTime.UtcNow },
            new Notification { Text = "Processing", AgentId = (int)AgentType.ClaudeCode, NotificationStatusId = (int)NotificationStatusEnum.Processing, CreatedAt = DateTime.UtcNow },
            new Notification { Text = "Played", AgentId = (int)AgentType.ClaudeCode, NotificationStatusId = (int)NotificationStatusEnum.Played, CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var handler = new GetNewNotificationsQueryHandler(context);
        var query = new GetNewNotificationsQuery();

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, n => Assert.Equal((int)NotificationStatusEnum.NewlyReceived, n.NotificationStatusId));
    }

    [Fact]
    public async Task GetNewNotifications_ReturnsEmptyList_WhenNoNewNotifications()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        context.Notifications.Add(
            new Notification { Text = "Played", AgentId = (int)AgentType.ClaudeCode, NotificationStatusId = (int)NotificationStatusEnum.Played, CreatedAt = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        var handler = new GetNewNotificationsQueryHandler(context);
        var query = new GetNewNotificationsQuery();

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetNewNotifications_OrdersByCreatedAt()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var olderTime = DateTime.UtcNow.AddMinutes(-10);
        var newerTime = DateTime.UtcNow;

        context.Notifications.AddRange(
            new Notification { Text = "Newer", AgentId = (int)AgentType.ClaudeCode, NotificationStatusId = (int)NotificationStatusEnum.NewlyReceived, CreatedAt = newerTime },
            new Notification { Text = "Older", AgentId = (int)AgentType.ClaudeCode, NotificationStatusId = (int)NotificationStatusEnum.NewlyReceived, CreatedAt = olderTime }
        );
        await context.SaveChangesAsync();

        var handler = new GetNewNotificationsQueryHandler(context);
        var query = new GetNewNotificationsQuery();

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("Older", result[0].Text);
        Assert.Equal("Newer", result[1].Text);
    }

    #endregion

    #region GetAssociatedIssueIdsQueryHandler Tests

    [Fact]
    public async Task GetAssociatedIssueIds_ReturnsDistinctIssueIds()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var repo = new GitHubRepository { Owner = "test", Name = "repo" };
        context.GitHubRepositories.Add(repo);
        await context.SaveChangesAsync();

        var issue1 = new GitHubIssue { RepositoryId = repo.Id, IssueNumber = 100 };
        var issue2 = new GitHubIssue { RepositoryId = repo.Id, IssueNumber = 200 };
        context.GitHubIssues.AddRange(issue1, issue2);
        await context.SaveChangesAsync();

        var notification1 = new Notification { Text = "N1", AgentId = (int)AgentType.ClaudeCode, NotificationStatusId = (int)NotificationStatusEnum.NewlyReceived, CreatedAt = DateTime.UtcNow };
        var notification2 = new Notification { Text = "N2", AgentId = (int)AgentType.ClaudeCode, NotificationStatusId = (int)NotificationStatusEnum.NewlyReceived, CreatedAt = DateTime.UtcNow };
        context.Notifications.AddRange(notification1, notification2);
        await context.SaveChangesAsync();

        // Link notifications to issues (both notifications linked to issue1, only notification2 to issue2)
        context.NotificationGitHubIssues.AddRange(
            new NotificationGitHubIssue { NotificationId = notification1.Id, GitHubIssueId = issue1.Id },
            new NotificationGitHubIssue { NotificationId = notification2.Id, GitHubIssueId = issue1.Id },
            new NotificationGitHubIssue { NotificationId = notification2.Id, GitHubIssueId = issue2.Id }
        );
        await context.SaveChangesAsync();

        var handler = new GetAssociatedIssueIdsQueryHandler(context);
        var query = new GetAssociatedIssueIdsQuery([notification1.Id, notification2.Id]);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(issue1.Id, result);
        Assert.Contains(issue2.Id, result);
    }

    [Fact]
    public async Task GetAssociatedIssueIds_ReturnsEmpty_WhenNoNotificationsLinked()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var notification = new Notification { Text = "N1", AgentId = (int)AgentType.ClaudeCode, NotificationStatusId = (int)NotificationStatusEnum.NewlyReceived, CreatedAt = DateTime.UtcNow };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        var handler = new GetAssociatedIssueIdsQueryHandler(context);
        var query = new GetAssociatedIssueIdsQuery([notification.Id]);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAssociatedIssueIds_ReturnsEmpty_WhenNotificationIdsEmpty()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        var handler = new GetAssociatedIssueIdsQueryHandler(context);
        var query = new GetAssociatedIssueIdsQuery([]);

        // Act
        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    #endregion
}
