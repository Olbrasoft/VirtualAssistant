using Microsoft.Extensions.Logging;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Data.Commands.NotificationCommands;
using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Data.Enums;
using Olbrasoft.VirtualAssistant.Data.Queries.NotificationQueries;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Service for managing notifications in the database.
/// Uses CQRS pattern for data access.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ICommandExecutor _commandExecutor;
    private readonly IQueryProcessor _queryProcessor;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ICommandExecutor commandExecutor,
        IQueryProcessor queryProcessor,
        ILogger<NotificationService> logger)
    {
        _commandExecutor = commandExecutor;
        _queryProcessor = queryProcessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> CreateNotificationAsync(string text, string agentName, IReadOnlyList<int>? issueIds = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text, nameof(text));
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName, nameof(agentName));

        var command = new CreateNotificationCommand(text, agentName, issueIds);
        var notificationId = await _commandExecutor.ExecuteAsync(command, ct);

        if (issueIds is { Count: > 0 })
        {
            _logger.LogInformation("Created notification {Id} from agent {AgentName} with {IssueCount} linked issues",
                notificationId, agentName, issueIds.Count);
        }
        else
        {
            _logger.LogInformation("Created notification {Id} from agent {AgentName}",
                notificationId, agentName);
        }

        return notificationId;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Notification>> GetNewNotificationsAsync(CancellationToken ct = default)
    {
        var query = new GetNewNotificationsQuery();
        return await _queryProcessor.ProcessAsync(query, ct);
    }

    /// <inheritdoc />
    public async Task UpdateStatusAsync(int notificationId, NotificationStatusEnum newStatus, CancellationToken ct = default)
    {
        var command = new UpdateNotificationStatusCommand(notificationId, newStatus);
        var success = await _commandExecutor.ExecuteAsync(command, ct);

        if (!success)
        {
            _logger.LogWarning("Notification {Id} not found for status update", notificationId);
        }
        else
        {
            _logger.LogDebug("Notification {Id} status changed to {NewStatus}", notificationId, newStatus);
        }
    }

    /// <inheritdoc />
    public async Task UpdateStatusAsync(IEnumerable<int> notificationIds, NotificationStatusEnum newStatus, CancellationToken ct = default)
    {
        var ids = notificationIds.ToList();
        if (ids.Count == 0) return;

        var command = new UpdateNotificationStatusBatchCommand(ids, newStatus);
        var count = await _commandExecutor.ExecuteAsync(command, ct);

        _logger.LogDebug("Updated {Count} notifications to status {NewStatus}", count, newStatus);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<int>> GetAssociatedIssueIdsAsync(IEnumerable<int> notificationIds, CancellationToken ct = default)
    {
        var ids = notificationIds.ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var query = new GetAssociatedIssueIdsQuery(ids);
        var issueIds = await _queryProcessor.ProcessAsync(query, ct);

        _logger.LogDebug("Found {IssueCount} associated GitHub issues for {NotificationCount} notifications",
            issueIds.Count, ids.Count);

        return issueIds;
    }

    /// <inheritdoc />
    public async Task RecordTtsOutcomeAsync(int notificationId, string? providerName, string status, int? durationMs = null, CancellationToken ct = default)
    {
        var command = new RecordTtsOutcomeCommand(notificationId, providerName, status, durationMs);
        var success = await _commandExecutor.ExecuteAsync(command, ct);

        if (!success)
        {
            _logger.LogWarning("Notification {Id} not found for TTS tracking", notificationId);
        }
        else
        {
            _logger.LogDebug("Recorded TTS outcome for notification {Id}: {Status} via {Provider} ({Duration}ms)",
                notificationId, status, providerName ?? "none", durationMs ?? 0);
        }
    }
}
