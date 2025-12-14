using Olbrasoft.Data.Entities.Abstractions;

namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Represents a notification from an agent stored in the database.
/// </summary>
public class Notification : BaseEnity
{
    /// <summary>
    /// Notification content/text.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to Agent (required).
    /// </summary>
    public int AgentId { get; set; }

    /// <summary>
    /// Navigation property to the agent.
    /// </summary>
    public Agent Agent { get; set; } = null!;

    /// <summary>
    /// When the notification was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Foreign key to NotificationStatus.
    /// </summary>
    public int NotificationStatusId { get; set; }

    /// <summary>
    /// Navigation property to the notification status.
    /// </summary>
    public NotificationStatus Status { get; set; } = null!;
}
