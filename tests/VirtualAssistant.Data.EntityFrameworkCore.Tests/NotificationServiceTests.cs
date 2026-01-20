using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Data.Commands.NotificationCommands;
using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Data.Enums;
using Olbrasoft.VirtualAssistant.Data.Queries.NotificationQueries;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.Tests;

/// <summary>
/// Unit tests for NotificationService using mocked CQRS infrastructure.
/// Tests verify that the service correctly delegates to CQRS commands and queries.
/// </summary>
public class NotificationServiceTests
{
    private readonly Mock<ICommandExecutor> _mockCommandExecutor;
    private readonly Mock<IQueryProcessor> _mockQueryProcessor;
    private readonly Mock<ILogger<NotificationService>> _mockLogger;
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        _mockCommandExecutor = new Mock<ICommandExecutor>();
        _mockQueryProcessor = new Mock<IQueryProcessor>();
        _mockLogger = new Mock<ILogger<NotificationService>>();
        _service = new NotificationService(_mockCommandExecutor.Object, _mockQueryProcessor.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task CreateNotificationAsync_ExecutesCommand_ReturnsNotificationId()
    {
        // Arrange
        const int expectedId = 42;
        _mockCommandExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<CreateNotificationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedId);

        // Act
        var result = await _service.CreateNotificationAsync("Test", "claude-code", null, null, null, CancellationToken.None);

        // Assert
        Assert.Equal(expectedId, result);
        _mockCommandExecutor.Verify(x => x.ExecuteAsync(
            It.Is<CreateNotificationCommand>(c => c.Text == "Test" && c.AgentName == "claude-code"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateNotificationAsync_WithIssueIds_PassesIssueIdsToCommand()
    {
        // Arrange
        var issueIds = new List<int> { 1, 2, 3 };
        _mockCommandExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<CreateNotificationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _service.CreateNotificationAsync("Test", "claude-code", issueIds, null, null, CancellationToken.None);

        // Assert
        _mockCommandExecutor.Verify(x => x.ExecuteAsync(
            It.Is<CreateNotificationCommand>(c => c.IssueIds != null && c.IssueIds.SequenceEqual(issueIds)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateNotificationAsync_WithLlmInfo_PassesLlmInfoToCommand()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<CreateNotificationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        await _service.CreateNotificationAsync("Test", "claude-code", null, "anthropic", "claude-opus-4-5", CancellationToken.None);

        // Assert
        _mockCommandExecutor.Verify(x => x.ExecuteAsync(
            It.Is<CreateNotificationCommand>(c =>
                c.ProviderName == "anthropic" &&
                c.ModelName == "claude-opus-4-5"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateNotificationAsync_PassesNullTextToCommand_ValidationInHandler()
    {
        // Arrange - validation is in handler, service just delegates
        _mockCommandExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<CreateNotificationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("text", "Text cannot be null"));

        // Act & Assert - exception comes from handler, not service
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateNotificationAsync(null!, "claude-code"));
    }

    [Fact]
    public async Task CreateNotificationAsync_PassesNullAgentNameToCommand_ValidationInHandler()
    {
        // Arrange - validation is in handler, service just delegates
        _mockCommandExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<CreateNotificationCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("agentName", "AgentName cannot be null"));

        // Act & Assert - exception comes from handler, not service
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateNotificationAsync("Test", null!));
    }

    [Fact]
    public async Task GetNewNotificationsAsync_ExecutesQuery_ReturnsNotifications()
    {
        // Arrange
        var notifications = new List<Notification>
        {
            new() { Id = 1, Text = "Test1" },
            new() { Id = 2, Text = "Test2" }
        };
        _mockQueryProcessor
            .Setup(x => x.ProcessAsync(It.IsAny<GetNewNotificationsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(notifications);

        // Act
        var result = await _service.GetNewNotificationsAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count);
        _mockQueryProcessor.Verify(x => x.ProcessAsync(
            It.IsAny<GetNewNotificationsQuery>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_SingleId_ExecutesCommand()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<UpdateNotificationStatusCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _service.UpdateStatusAsync(1, NotificationStatusEnum.Processing, CancellationToken.None);

        // Assert
        _mockCommandExecutor.Verify(x => x.ExecuteAsync(
            It.Is<UpdateNotificationStatusCommand>(c => c.NotificationId == 1 && c.NewStatus == NotificationStatusEnum.Processing),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_BatchIds_ExecutesBatchCommand()
    {
        // Arrange
        var ids = new[] { 1, 2, 3 };
        _mockCommandExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<UpdateNotificationStatusBatchCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        // Act
        await _service.UpdateStatusAsync(ids, NotificationStatusEnum.Processing, CancellationToken.None);

        // Assert
        _mockCommandExecutor.Verify(x => x.ExecuteAsync(
            It.Is<UpdateNotificationStatusBatchCommand>(c => c.NotificationIds.Count == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_EmptyBatch_DoesNotExecuteCommand()
    {
        // Act
        await _service.UpdateStatusAsync(Array.Empty<int>(), NotificationStatusEnum.Processing, CancellationToken.None);

        // Assert
        _mockCommandExecutor.Verify(x => x.ExecuteAsync(
            It.IsAny<UpdateNotificationStatusBatchCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAssociatedIssueIdsAsync_ExecutesQuery_ReturnsIssueIds()
    {
        // Arrange
        var issueIds = new List<int> { 100, 200, 300 };
        _mockQueryProcessor
            .Setup(x => x.ProcessAsync(It.IsAny<GetAssociatedIssueIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(issueIds);

        // Act
        var result = await _service.GetAssociatedIssueIdsAsync([1, 2], CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Count);
        _mockQueryProcessor.Verify(x => x.ProcessAsync(
            It.Is<GetAssociatedIssueIdsQuery>(q => q.NotificationIds.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAssociatedIssueIdsAsync_EmptyInput_ReturnsEmpty()
    {
        // Act
        var result = await _service.GetAssociatedIssueIdsAsync(Array.Empty<int>(), CancellationToken.None);

        // Assert
        Assert.Empty(result);
        _mockQueryProcessor.Verify(x => x.ProcessAsync(
            It.IsAny<GetAssociatedIssueIdsQuery>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordTtsOutcomeAsync_ExecutesCommand()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.ExecuteAsync(It.IsAny<RecordTtsOutcomeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _service.RecordTtsOutcomeAsync(1, "AzureTTS", "success", 500, CancellationToken.None);

        // Assert
        _mockCommandExecutor.Verify(x => x.ExecuteAsync(
            It.Is<RecordTtsOutcomeCommand>(c =>
                c.NotificationId == 1 &&
                c.ProviderName == "AzureTTS" &&
                c.Status == "success" &&
                c.DurationMs == 500),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
