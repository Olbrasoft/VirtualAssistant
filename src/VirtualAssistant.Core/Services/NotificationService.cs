using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VirtualAssistant.Data.Entities;
using VirtualAssistant.Data.EntityFrameworkCore;

namespace VirtualAssistant.Core.Services;

/// <summary>
/// Service for managing notifications in the database.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly VirtualAssistantDbContext _dbContext;
    private readonly ILogger<NotificationService> _logger;

    /// <summary>
    /// Status ID for newly received notifications.
    /// </summary>
    private const int NewlyReceivedStatusId = 1;

    public NotificationService(
        VirtualAssistantDbContext dbContext,
        ILogger<NotificationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> CreateNotificationAsync(string text, string agentId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text, nameof(text));
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId, nameof(agentId));

        var notification = new Notification
        {
            Text = text,
            AgentId = agentId,
            CreatedAt = DateTime.UtcNow,
            NotificationStatusId = NewlyReceivedStatusId
        };

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created notification {Id} from agent {Agent}", notification.Id, agentId);

        return notification.Id;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Notification>> GetNewNotificationsAsync(CancellationToken ct = default)
    {
        return await _dbContext.Notifications
            .Where(n => n.NotificationStatusId == NewlyReceivedStatusId)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync(ct);
    }
}
