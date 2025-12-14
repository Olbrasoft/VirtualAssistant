using Olbrasoft.Data.Entities.Abstractions;

namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Reference table for notification statuses.
/// Predefined statuses: NewlyReceived, Announced, WaitingForPlayback.
/// </summary>
public class NotificationStatus : BaseEnity
{
    /// <summary>
    /// Status name (e.g., "NewlyReceived", "Announced", "WaitingForPlayback").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Notifications with this status.
    /// </summary>
    public ICollection<Notification> Notifications { get; set; } = [];
}
