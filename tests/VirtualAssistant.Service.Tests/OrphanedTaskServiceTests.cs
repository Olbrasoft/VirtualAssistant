using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Service.Services;
using VirtualAssistant.Core.Services;
using VirtualAssistant.Data.Entities;
using VirtualAssistant.Data.EntityFrameworkCore;
using VirtualAssistant.Data.Enums;
using VirtualAssistant.GitHub.Services;

namespace VirtualAssistant.Service.Tests;

public class OrphanedTaskServiceTests : IDisposable
{
    private readonly VirtualAssistantDbContext _dbContext;
    private readonly Mock<IGitHubIssueStatusService> _githubServiceMock;
    private readonly Mock<ILogger<OrphanedTaskService>> _loggerMock;
    private readonly OrphanedTaskService _service;

    public OrphanedTaskServiceTests()
    {
        var options = new DbContextOptionsBuilder<VirtualAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new VirtualAssistantDbContext(options);
        _githubServiceMock = new Mock<IGitHubIssueStatusService>();
        _loggerMock = new Mock<ILogger<OrphanedTaskService>>();

        _service = new OrphanedTaskService(_dbContext, _githubServiceMock.Object, _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task FindOrphanedTasksAsync_NoStuckResponses_ReturnsEmpty()
    {
        // Arrange - no data

        // Act
        var result = await _service.FindOrphanedTasksAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task FindOrphanedTasksAsync_StuckResponse_ReturnsOrphanedTask()
    {
        // Arrange
        var response = new AgentResponse
        {
            AgentName = "claude",
            Status = AgentResponseStatus.InProgress,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = null
        };
        _dbContext.AgentResponses.Add(response);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.FindOrphanedTasksAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("claude", result[0].AgentName);
        Assert.Equal("idle", result[0].AgentStatus);
    }

    [Fact]
    public async Task FindOrphanedTasksAsync_StuckResponseWithTask_IncludesTaskInfo()
    {
        // Arrange
        var task = new AgentTask
        {
            GithubIssueNumber = 215,
            Summary = "Test task",
            Status = "sent",
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        };
        _dbContext.AgentTasks.Add(task);
        await _dbContext.SaveChangesAsync();

        var response = new AgentResponse
        {
            AgentName = "claude",
            Status = AgentResponseStatus.InProgress,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = null,
            AgentTaskId = task.Id
        };
        _dbContext.AgentResponses.Add(response);
        await _dbContext.SaveChangesAsync();

        _githubServiceMock
            .Setup(x => x.GetIssueStatusAsync("Olbrasoft", "VirtualAssistant", 215, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitHubIssueStatus { Number = 215, State = "open", Found = true });

        // Act
        var result = await _service.FindOrphanedTasksAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(215, result[0].GithubIssueNumber);
        Assert.Equal("Test task", result[0].TaskSummary);
        Assert.Equal("open", result[0].GithubIssueStatus);
    }

    [Fact]
    public async Task FindOrphanedTasksAsync_CompletedResponse_NotIncluded()
    {
        // Arrange
        var response = new AgentResponse
        {
            AgentName = "claude",
            Status = AgentResponseStatus.Completed,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow
        };
        _dbContext.AgentResponses.Add(response);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.FindOrphanedTasksAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task MarkAsCompletedAsync_CompletesResponseAndTask()
    {
        // Arrange
        var task = new AgentTask
        {
            GithubIssueNumber = 215,
            Summary = "Test task",
            Status = "sent",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.AgentTasks.Add(task);
        await _dbContext.SaveChangesAsync();

        var response = new AgentResponse
        {
            AgentName = "claude",
            Status = AgentResponseStatus.InProgress,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = null,
            AgentTaskId = task.Id
        };
        _dbContext.AgentResponses.Add(response);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.MarkAsCompletedAsync(response.Id);

        // Assert
        var updatedResponse = await _dbContext.AgentResponses.FindAsync(response.Id);
        var updatedTask = await _dbContext.AgentTasks.FindAsync(task.Id);

        Assert.Equal(AgentResponseStatus.Completed, updatedResponse!.Status);
        Assert.NotNull(updatedResponse.CompletedAt);
        Assert.Equal("completed", updatedTask!.Status);
        Assert.NotNull(updatedTask.CompletedAt);
    }

    [Fact]
    public async Task ResetTaskAsync_ResetsTaskToPending()
    {
        // Arrange
        var task = new AgentTask
        {
            GithubIssueNumber = 215,
            Summary = "Test task",
            Status = "sent",
            SentAt = DateTime.UtcNow.AddHours(-1),
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        };
        _dbContext.AgentTasks.Add(task);
        await _dbContext.SaveChangesAsync();

        var response = new AgentResponse
        {
            AgentName = "claude",
            Status = AgentResponseStatus.InProgress,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = null,
            AgentTaskId = task.Id
        };
        _dbContext.AgentResponses.Add(response);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.ResetTaskAsync(response.Id);

        // Assert
        var updatedResponse = await _dbContext.AgentResponses.FindAsync(response.Id);
        var updatedTask = await _dbContext.AgentTasks.FindAsync(task.Id);

        Assert.Equal(AgentResponseStatus.Completed, updatedResponse!.Status);
        Assert.Equal("pending", updatedTask!.Status);
        Assert.Null(updatedTask.SentAt);
    }

    [Fact]
    public async Task IgnoreAsync_CompletesResponseOnly()
    {
        // Arrange
        var task = new AgentTask
        {
            GithubIssueNumber = 215,
            Summary = "Test task",
            Status = "sent",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.AgentTasks.Add(task);
        await _dbContext.SaveChangesAsync();

        var response = new AgentResponse
        {
            AgentName = "claude",
            Status = AgentResponseStatus.InProgress,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = null,
            AgentTaskId = task.Id
        };
        _dbContext.AgentResponses.Add(response);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.IgnoreAsync(response.Id);

        // Assert
        var updatedResponse = await _dbContext.AgentResponses.FindAsync(response.Id);
        var updatedTask = await _dbContext.AgentTasks.FindAsync(task.Id);

        Assert.Equal(AgentResponseStatus.Completed, updatedResponse!.Status);
        Assert.Equal("sent", updatedTask!.Status); // Task unchanged
    }
}
