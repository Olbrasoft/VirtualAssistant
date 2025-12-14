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
    /// Identifier of the agent sending the notification.
    /// Examples: "OpenCode", "Claude Code", "Antigravity", "Gemini".
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

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
