using Microsoft.EntityFrameworkCore;
using Olbrasoft.VirtualAssistant.Data.Commands.NotificationCommands;
using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Data.Enums;
using Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers.NotificationCommandHandlers;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Tests.CommandHandlers;

/// <summary>
/// Unit tests for Notification command handlers using in-memory database.
/// </summary>
public class NotificationCommandHandlerTests
{
    private static VirtualAssistantDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<VirtualAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new VirtualAssistantDbContext(options);

        // Seed required data
        SeedRequiredData(context);

        return context;
    }

    private static void SeedRequiredData(VirtualAssistantDbContext context)
    {
        // Add required notification statuses
        context.NotificationStatuses.AddRange(
            new NotificationStatus { Id = (int)NotificationStatusEnum.NewlyReceived, Name = "NewlyReceived" },
            new NotificationStatus { Id = (int)NotificationStatusEnum.Processing, Name = "Processing" },
            new NotificationStatus { Id = (int)NotificationStatusEnum.Summarized, Name = "Summarized" },
            new NotificationStatus { Id = (int)NotificationStatusEnum.Played, Name = "Played" }
        );

        // Add required agents
        context.Agents.AddRange(
            new Agent { Id = (int)AgentType.OpenCode, Name = "OpenCode" },
            new Agent { Id = (int)AgentType.ClaudeCode, Name = "Claude Code" },
            new Agent { Id = (int)AgentType.Gemini, Name = "Gemini" }
        );

        context.SaveChanges();
    }

    #region CreateNotificationCommandHandler Tests

    [Fact]
    public async Task CreateHandler_WithValidData_ReturnsNotificationId()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var handler = new CreateNotificationCommandHandler(context);
        var command = new CreateNotificationCommand("Test notification", "claude-code", null);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result > 0);
    }

    [Fact]
    public async Task CreateHandler_WithValidData_SavesNotification()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var handler = new CreateNotificationCommandHandler(context);
        var command = new CreateNotificationCommand("Test text", "claude-code", null);

        // Act
        var id = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        var saved = await context.Notifications.FindAsync(id);
        Assert.NotNull(saved);
        Assert.Equal("Test text", saved.Text);
        Assert.Equal((int)AgentType.ClaudeCode, saved.AgentId);
        Assert.Equal((int)NotificationStatusEnum.NewlyReceived, saved.NotificationStatusId);
    }

    [Fact]
    public async Task CreateHandler_WithIssueIds_CreatesNotificationGitHubIssues()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        // Add required GitHub issues
        var repo = new GitHubRepository { Owner = "test", Name = "repo" };
        context.GitHubRepositories.Add(repo);
        await context.SaveChangesAsync();

        var issue1 = new GitHubIssue { RepositoryId = repo.Id, IssueNumber = 100 };
        var issue2 = new GitHubIssue { RepositoryId = repo.Id, IssueNumber = 200 };
        context.GitHubIssues.AddRange(issue1, issue2);
        await context.SaveChangesAsync();

        var handler = new CreateNotificationCommandHandler(context);
        var command = new CreateNotificationCommand("With issues", "claude-code", [issue1.Id, issue2.Id]);

        // Act
        var id = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        var links = await context.NotificationGitHubIssues
            .Where(n => n.NotificationId == id)
            .ToListAsync();
        Assert.Equal(2, links.Count);
    }

    [Theory]
    [InlineData("opencode", AgentType.OpenCode)]
    [InlineData("claude", AgentType.ClaudeCode)]
    [InlineData("claude-code", AgentType.ClaudeCode)]
    [InlineData("gemini", AgentType.Gemini)]
    public async Task CreateHandler_MapsAgentNameCorrectly(string agentName, AgentType expectedType)
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var handler = new CreateNotificationCommandHandler(context);
        var command = new CreateNotificationCommand("Test", agentName, null);

        // Act
        var id = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        var saved = await context.Notifications.FindAsync(id);
        Assert.NotNull(saved);
        Assert.Equal((int)expectedType, saved.AgentId);
    }

    [Fact]
    public async Task CreateHandler_WithInvalidAgentName_ThrowsArgumentException()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var handler = new CreateNotificationCommandHandler(context);
        var command = new CreateNotificationCommand("Test", "invalid-agent", null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task CreateHandler_WithNullText_ThrowsArgumentNullException()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var handler = new CreateNotificationCommandHandler(context);
        var command = new CreateNotificationCommand(null!, "claude-code", null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task CreateHandler_WithEmptyText_ThrowsArgumentException()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var handler = new CreateNotificationCommandHandler(context);
        var command = new CreateNotificationCommand("", "claude-code", null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(command, CancellationToken.None));
    }

    #endregion

    #region UpdateNotificationStatusCommandHandler Tests

    [Fact]
    public async Task UpdateStatusHandler_WithExistingNotification_ReturnsTrue()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var notification = new Notification
        {
            Text = "Test",
            AgentId = (int)AgentType.ClaudeCode,
            NotificationStatusId = (int)NotificationStatusEnum.NewlyReceived,
            CreatedAt = DateTime.UtcNow
        };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        var handler = new UpdateNotificationStatusCommandHandler(context);
        var command = new UpdateNotificationStatusCommand(notification.Id, NotificationStatusEnum.Processing);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task UpdateStatusHandler_UpdatesStatus()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var notification = new Notification
        {
            Text = "Test",
            AgentId = (int)AgentType.ClaudeCode,
            NotificationStatusId = (int)NotificationStatusEnum.NewlyReceived,
            CreatedAt = DateTime.UtcNow
        };
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        var handler = new UpdateNotificationStatusCommandHandler(context);
        var command = new UpdateNotificationStatusCommand(notification.Id, NotificationStatusEnum.Played);

        // Act
        await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        var updated = await context.Notifications.FindAsync(notification.Id);
        Assert.NotNull(updated);
        Assert.Equal((int)NotificationStatusEnum.Played, updated.NotificationStatusId);
    }

    [Fact]
    public async Task UpdateStatusHandler_WithNonExistingNotification_ReturnsFalse()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var handler = new UpdateNotificationStatusCommandHandler(context);
        var command = new UpdateNotificationStatusCommand(999, NotificationStatusEnum.Processing);

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region UpdateNotificationStatusBatchCommandHandler Tests

    [Fact]
    public async Task UpdateStatusBatchHandler_UpdatesMultipleNotifications()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var notifications = new[]
        {
            new Notification { Text = "N1", AgentId = (int)AgentType.ClaudeCode, NotificationStatusId = (int)NotificationStatusEnum.NewlyReceived, CreatedAt = DateTime.UtcNow },
            new Notification { Text = "N2", AgentId = (int)AgentType.ClaudeCode, NotificationStatusId = (int)NotificationStatusEnum.NewlyReceived, CreatedAt = DateTime.UtcNow },
            new Notification { Text = "N3", AgentId = (int)AgentType.ClaudeCode, NotificationStatusId = (int)NotificationStatusEnum.NewlyReceived, CreatedAt = DateTime.UtcNow }
        };
        context.Notifications.AddRange(notifications);
        await context.SaveChangesAsync();

        var ids = notifications.Select(n => n.Id).ToList();
        var handler = new UpdateNotificationStatusBatchCommandHandler(context);
        var command = new UpdateNotificationStatusBatchCommand(ids, NotificationStatusEnum.Processing);

        // Act
        var count = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(3, count);
        foreach (var notification in notifications)
        {
            var updated = await context.Notifications.FindAsync(notification.Id);
            Assert.NotNull(updated);
            Assert.Equal((int)NotificationStatusEnum.Processing, updated.NotificationStatusId);
        }
    }

    [Fact]
    public async Task UpdateStatusBatchHandler_WithEmptyList_ReturnsZero()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        var handler = new UpdateNotificationStatusBatchCommandHandler(context);
        var command = new UpdateNotificationStatusBatchCommand([], NotificationStatusEnum.Processing);

        // Act
        var count = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.Equal(0, count);
    }

    #endregion
}
