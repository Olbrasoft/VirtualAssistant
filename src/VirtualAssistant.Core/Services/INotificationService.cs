using VirtualAssistant.Data.Entities;
using VirtualAssistant.Data.Enums;

namespace VirtualAssistant.Core.Services;

/// <summary>
/// Service for managing notifications in the database.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Creates a new notification with NewlyReceived status.
    /// </summary>
    /// <param name="text">Notification text content</param>
    /// <param name="agentName">Agent name (e.g., "opencode", "claude") - will be looked up in database</param>
    /// <param name="issueIds">Optional GitHub issue IDs to associate with this notification</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The ID of the created notification</returns>
    Task<int> CreateNotificationAsync(string text, string agentName, IReadOnlyList<int>? issueIds = null, CancellationToken ct = default);

    /// <summary>
    /// Gets all notifications with NewlyReceived status.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of new notifications</returns>
    Task<IReadOnlyList<Notification>> GetNewNotificationsAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates the status of a notification.
    /// </summary>
    /// <param name="notificationId">ID of the notification to update</param>
    /// <param name="newStatus">New status to set</param>
    /// <param name="ct">Cancellation token</param>
    Task UpdateStatusAsync(int notificationId, NotificationStatusEnum newStatus, CancellationToken ct = default);

    /// <summary>
    /// Updates the status of multiple notifications.
    /// </summary>
    /// <param name="notificationIds">IDs of notifications to update</param>
    /// <param name="newStatus">New status to set</param>
    /// <param name="ct">Cancellation token</param>
    Task UpdateStatusAsync(IEnumerable<int> notificationIds, NotificationStatusEnum newStatus, CancellationToken ct = default);
}
