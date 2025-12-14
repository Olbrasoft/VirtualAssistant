using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VirtualAssistant.Data.Entities;
using VirtualAssistant.Data.EntityFrameworkCore;
using VirtualAssistant.Data.Enums;

namespace VirtualAssistant.Core.Services;

/// <summary>
/// Service for managing notifications in the database.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly VirtualAssistantDbContext _dbContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        VirtualAssistantDbContext dbContext,
        ILogger<NotificationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> CreateNotificationAsync(string text, string agentName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text, nameof(text));
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName, nameof(agentName));

        // Normalize agent name to lowercase for lookup
        var normalizedName = agentName.ToLowerInvariant();

        var agent = await _dbContext.Agents
            .FirstOrDefaultAsync(a => a.Name.ToLower() == normalizedName, ct);

        if (agent == null)
        {
            _logger.LogWarning("Agent '{AgentName}' not found in database, creating new agent", agentName);
            agent = new Agent
            {
                Name = normalizedName,
                Label = $"agent:{normalizedName}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Agents.Add(agent);
            await _dbContext.SaveChangesAsync(ct);
        }

        var notification = new Notification
        {
            Text = text,
            AgentId = agent.Id,
            CreatedAt = DateTime.UtcNow,
            NotificationStatusId = (int)NotificationStatusEnum.NewlyReceived
        };

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created notification {Id} from agent {AgentName} (ID: {AgentId})", notification.Id, agentName, agent.Id);

        return notification.Id;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Notification>> GetNewNotificationsAsync(CancellationToken ct = default)
    {
        return await _dbContext.Notifications
            .Where(n => n.NotificationStatusId == (int)NotificationStatusEnum.NewlyReceived)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task UpdateStatusAsync(int notificationId, NotificationStatusEnum newStatus, CancellationToken ct = default)
    {
        var notification = await _dbContext.Notifications.FindAsync([notificationId], ct);
        if (notification == null)
        {
            _logger.LogWarning("Notification {Id} not found for status update", notificationId);
            return;
        }

        var oldStatus = (NotificationStatusEnum)notification.NotificationStatusId;
        notification.NotificationStatusId = (int)newStatus;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogDebug("Notification {Id} status changed: {OldStatus} -> {NewStatus}",
            notificationId, oldStatus, newStatus);
    }

    /// <inheritdoc />
    public async Task UpdateStatusAsync(IEnumerable<int> notificationIds, NotificationStatusEnum newStatus, CancellationToken ct = default)
    {
        var ids = notificationIds.ToList();
        if (ids.Count == 0) return;

        var notifications = await _dbContext.Notifications
            .Where(n => ids.Contains(n.Id))
            .ToListAsync(ct);

        foreach (var notification in notifications)
        {
            notification.NotificationStatusId = (int)newStatus;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogDebug("Updated {Count} notifications to status {NewStatus}", notifications.Count, newStatus);
    }
}
