using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using VirtualAssistant.Core.Services;
using VirtualAssistant.Data.Entities;
using VirtualAssistant.Data.Enums;

namespace VirtualAssistant.Data.EntityFrameworkCore.Tests;

public class NotificationServiceTests
{
    private VirtualAssistantDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<VirtualAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new VirtualAssistantDbContext(options);
    }

    private async Task SeedAgentAsync(VirtualAssistantDbContext context, string name = "claude-code")
    {
        context.Agents.Add(new Agent
        {
            Name = name,
            Label = $"agent:{name}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateNotificationAsync_WithoutIssueIds_CreatesNotificationOnly()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedAgentAsync(context);
        var mockLogger = new Mock<ILogger<NotificationService>>();
        var service = new NotificationService(context, mockLogger.Object);

        // Act
        var notificationId = await service.CreateNotificationAsync(
            "Test notification",
            "claude-code",
            issueIds: null,
            CancellationToken.None);

        // Assert
        Assert.True(notificationId > 0);

        var notification = await context.Notifications.FindAsync(notificationId);
        Assert.NotNull(notification);
        Assert.Equal("Test notification", notification.Text);

        var junctionRecords = await context.NotificationGitHubIssues
            .Where(x => x.NotificationId == notificationId)
            .ToListAsync();
        Assert.Empty(junctionRecords);
    }

    [Fact]
    public async Task CreateNotificationAsync_WithSingleIssueId_CreatesJunctionRecord()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedAgentAsync(context);
        var mockLogger = new Mock<ILogger<NotificationService>>();
        var service = new NotificationService(context, mockLogger.Object);

        // Act
        var notificationId = await service.CreateNotificationAsync(
            "Working on issue",
            "claude-code",
            issueIds: [275],
            CancellationToken.None);

        // Assert
        Assert.True(notificationId > 0);

        var junctionRecords = await context.NotificationGitHubIssues
            .Where(x => x.NotificationId == notificationId)
            .ToListAsync();

        Assert.Single(junctionRecords);
        Assert.Equal(275, junctionRecords[0].GitHubIssueId);
    }

    [Fact]
    public async Task CreateNotificationAsync_WithMultipleIssueIds_CreatesAllJunctionRecords()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedAgentAsync(context);
        var mockLogger = new Mock<ILogger<NotificationService>>();
        var service = new NotificationService(context, mockLogger.Object);

        // Act
        var notificationId = await service.CreateNotificationAsync(
            "Working on multiple issues",
            "claude-code",
            issueIds: [273, 274, 275],
            CancellationToken.None);

        // Assert
        var junctionRecords = await context.NotificationGitHubIssues
            .Where(x => x.NotificationId == notificationId)
            .OrderBy(x => x.GitHubIssueId)
            .ToListAsync();

        Assert.Equal(3, junctionRecords.Count);
        Assert.Equal(273, junctionRecords[0].GitHubIssueId);
        Assert.Equal(274, junctionRecords[1].GitHubIssueId);
        Assert.Equal(275, junctionRecords[2].GitHubIssueId);
    }

    [Fact]
    public async Task CreateNotificationAsync_WithDuplicateIssueIds_CreatesSingleJunctionRecord()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedAgentAsync(context);
        var mockLogger = new Mock<ILogger<NotificationService>>();
        var service = new NotificationService(context, mockLogger.Object);

        // Act
        var notificationId = await service.CreateNotificationAsync(
            "Duplicate issue IDs",
            "claude-code",
            issueIds: [275, 275, 275],
            CancellationToken.None);

        // Assert
        var junctionRecords = await context.NotificationGitHubIssues
            .Where(x => x.NotificationId == notificationId)
            .ToListAsync();

        Assert.Single(junctionRecords);
        Assert.Equal(275, junctionRecords[0].GitHubIssueId);
    }

    [Fact]
    public async Task CreateNotificationAsync_WithEmptyIssueIdsList_CreatesNoJunctionRecords()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedAgentAsync(context);
        var mockLogger = new Mock<ILogger<NotificationService>>();
        var service = new NotificationService(context, mockLogger.Object);

        // Act
        var notificationId = await service.CreateNotificationAsync(
            "Empty list",
            "claude-code",
            issueIds: [],
            CancellationToken.None);

        // Assert
        var junctionRecords = await context.NotificationGitHubIssues
            .Where(x => x.NotificationId == notificationId)
            .ToListAsync();

        Assert.Empty(junctionRecords);
    }

    [Fact]
    public async Task CreateNotificationAsync_JunctionRecordsHaveCorrectNotificationId()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedAgentAsync(context);
        var mockLogger = new Mock<ILogger<NotificationService>>();
        var service = new NotificationService(context, mockLogger.Object);

        // Act - create two notifications
        var notificationId1 = await service.CreateNotificationAsync(
            "First notification",
            "claude-code",
            issueIds: [100],
            CancellationToken.None);

        var notificationId2 = await service.CreateNotificationAsync(
            "Second notification",
            "claude-code",
            issueIds: [200],
            CancellationToken.None);

        // Assert - each notification has its own junction record
        var records1 = await context.NotificationGitHubIssues
            .Where(x => x.NotificationId == notificationId1)
            .ToListAsync();
        Assert.Single(records1);
        Assert.Equal(100, records1[0].GitHubIssueId);

        var records2 = await context.NotificationGitHubIssues
            .Where(x => x.NotificationId == notificationId2)
            .ToListAsync();
        Assert.Single(records2);
        Assert.Equal(200, records2[0].GitHubIssueId);
    }
}
