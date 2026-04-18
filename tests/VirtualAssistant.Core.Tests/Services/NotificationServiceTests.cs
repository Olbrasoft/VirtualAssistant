using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Data.Commands.NotificationCommands;
using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Data.Enums;
using Olbrasoft.VirtualAssistant.Data.Queries.NotificationQueries;

namespace Olbrasoft.VirtualAssistant.Core.Tests.Services;

/// <summary>
/// Pins the CQRS orchestration inside NotificationService: each public method
/// must forward to the right command/query, and the early-exit branches
/// (empty id lists on the batch-update and associated-issue queries) must
/// skip the executor entirely.
/// </summary>
public class NotificationServiceTests
{
    private readonly Mock<ICommandExecutor> _commandExecutorMock = new();
    private readonly Mock<IQueryProcessor> _queryProcessorMock = new();
    private readonly Mock<ILogger<NotificationService>> _loggerMock = new();

    private NotificationService CreateSut() =>
        new(_commandExecutorMock.Object, _queryProcessorMock.Object, _loggerMock.Object);

    [Fact]
    public async Task CreateNotificationAsync_ForwardsAllFieldsToCommand_AndReturnsNewId()
    {
        CreateNotificationCommand? captured = null;
        _commandExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ICommand<int>>(), It.IsAny<CancellationToken>()))
            .Callback<ICommand<int>, CancellationToken>((cmd, _) => captured = (CreateNotificationCommand)cmd)
            .ReturnsAsync(42);

        var sut = CreateSut();
        var issueIds = new[] { 1, 2, 3 };

        var id = await sut.CreateNotificationAsync(
            text: "hello",
            agentName: "claude-code",
            issueIds: issueIds,
            providerName: "anthropic",
            modelName: "claude-opus-4-7");

        Assert.Equal(42, id);
        Assert.NotNull(captured);
        Assert.Equal("hello", captured!.Text);
        Assert.Equal("claude-code", captured.AgentName);
        Assert.Equal(issueIds, captured.IssueIds);
        Assert.Equal("anthropic", captured.ProviderName);
        Assert.Equal("claude-opus-4-7", captured.ModelName);
    }

    [Fact]
    public async Task CreateNotificationAsync_WithoutOptionals_StillDispatchesCommand()
    {
        CreateNotificationCommand? captured = null;
        _commandExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ICommand<int>>(), It.IsAny<CancellationToken>()))
            .Callback<ICommand<int>, CancellationToken>((cmd, _) => captured = (CreateNotificationCommand)cmd)
            .ReturnsAsync(7);

        var sut = CreateSut();
        var id = await sut.CreateNotificationAsync("x", "opencode");

        Assert.Equal(7, id);
        Assert.Null(captured!.IssueIds);
        Assert.Null(captured.ProviderName);
        Assert.Null(captured.ModelName);
    }

    [Fact]
    public async Task GetNewNotificationsAsync_ForwardsQuery_AndReturnsResult()
    {
        var expected = new List<Notification>
        {
            new() { Id = 1, Text = "a" },
            new() { Id = 2, Text = "b" },
        };
        _queryProcessorMock
            .Setup(x => x.ProcessAsync(It.IsAny<IQuery<IReadOnlyList<Notification>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = CreateSut();
        var result = await sut.GetNewNotificationsAsync();

        Assert.Same(expected, result);
        _queryProcessorMock.Verify(
            x => x.ProcessAsync(It.IsAny<GetNewNotificationsQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_Single_ForwardsCommand_WithIdAndStatus()
    {
        UpdateNotificationStatusCommand? captured = null;
        _commandExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ICommand<bool>>(), It.IsAny<CancellationToken>()))
            .Callback<ICommand<bool>, CancellationToken>((cmd, _) => captured = cmd as UpdateNotificationStatusCommand)
            .ReturnsAsync(true);

        var sut = CreateSut();
        await sut.UpdateStatusAsync(99, NotificationStatusEnum.Played);

        Assert.NotNull(captured);
        Assert.Equal(99, captured!.NotificationId);
        Assert.Equal(NotificationStatusEnum.Played, captured.NewStatus);
    }

    [Fact]
    public async Task UpdateStatusAsync_Batch_EmptyIds_DoesNotInvokeExecutor()
    {
        var sut = CreateSut();

        await sut.UpdateStatusAsync(Array.Empty<int>(), NotificationStatusEnum.Played);

        _commandExecutorMock.Verify(
            x => x.ExecuteAsync(It.IsAny<ICommand<int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_Batch_WithIds_ForwardsBatchCommand()
    {
        UpdateNotificationStatusBatchCommand? captured = null;
        _commandExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ICommand<int>>(), It.IsAny<CancellationToken>()))
            .Callback<ICommand<int>, CancellationToken>((cmd, _) => captured = cmd as UpdateNotificationStatusBatchCommand)
            .ReturnsAsync(3);

        var sut = CreateSut();
        await sut.UpdateStatusAsync(new[] { 1, 2, 3 }, NotificationStatusEnum.Played);

        Assert.NotNull(captured);
        Assert.Equal(new[] { 1, 2, 3 }, captured!.NotificationIds);
        Assert.Equal(NotificationStatusEnum.Played, captured.NewStatus);
    }

    [Fact]
    public async Task GetAssociatedIssueIdsAsync_EmptyIds_ReturnsEmpty_WithoutQuerying()
    {
        var sut = CreateSut();

        var result = await sut.GetAssociatedIssueIdsAsync(Array.Empty<int>());

        Assert.Empty(result);
        _queryProcessorMock.Verify(
            x => x.ProcessAsync(It.IsAny<GetAssociatedIssueIdsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAssociatedIssueIdsAsync_WithIds_ForwardsQuery_AndReturnsResult()
    {
        IReadOnlyList<int> expected = new[] { 100, 200 };
        GetAssociatedIssueIdsQuery? captured = null;
        _queryProcessorMock
            .Setup(x => x.ProcessAsync(It.IsAny<IQuery<IReadOnlyList<int>>>(), It.IsAny<CancellationToken>()))
            .Callback<IQuery<IReadOnlyList<int>>, CancellationToken>((q, _) => captured = q as GetAssociatedIssueIdsQuery)
            .ReturnsAsync(expected);

        var sut = CreateSut();
        var result = await sut.GetAssociatedIssueIdsAsync(new[] { 5, 6 });

        Assert.Equal(expected, result);
        Assert.NotNull(captured);
        Assert.Equal(new[] { 5, 6 }, captured!.NotificationIds);
    }

    [Fact]
    public async Task RecordTtsOutcomeAsync_ForwardsAllFields_ToCommand()
    {
        RecordTtsOutcomeCommand? captured = null;
        _commandExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ICommand<bool>>(), It.IsAny<CancellationToken>()))
            .Callback<ICommand<bool>, CancellationToken>((cmd, _) => captured = cmd as RecordTtsOutcomeCommand)
            .ReturnsAsync(true);

        var sut = CreateSut();
        await sut.RecordTtsOutcomeAsync(77, "azure", "success", 250);

        Assert.NotNull(captured);
        Assert.Equal(77, captured!.NotificationId);
        Assert.Equal("azure", captured.ProviderName);
        Assert.Equal("success", captured.Status);
        Assert.Equal(250, captured.DurationMs);
    }

    [Fact]
    public async Task RecordTtsOutcomeAsync_WithNullProvider_AllowsNullProviderInCommand()
    {
        RecordTtsOutcomeCommand? captured = null;
        _commandExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ICommand<bool>>(), It.IsAny<CancellationToken>()))
            .Callback<ICommand<bool>, CancellationToken>((cmd, _) => captured = cmd as RecordTtsOutcomeCommand)
            .ReturnsAsync(false);

        var sut = CreateSut();
        await sut.RecordTtsOutcomeAsync(1, providerName: null, status: "all_failed");

        Assert.NotNull(captured);
        Assert.Null(captured!.ProviderName);
        Assert.Equal("all_failed", captured.Status);
        Assert.Null(captured.DurationMs);
    }
}
