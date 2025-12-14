using VirtualAssistant.Data.Entities;

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
    /// <param name="ct">Cancellation token</param>
    /// <returns>The ID of the created notification</returns>
    Task<int> CreateNotificationAsync(string text, string agentName, CancellationToken ct = default);

    /// <summary>
    /// Gets all notifications with NewlyReceived status.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of new notifications</returns>
    Task<IReadOnlyList<Notification>> GetNewNotificationsAsync(CancellationToken ct = default);
}
