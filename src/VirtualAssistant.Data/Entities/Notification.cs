using Olbrasoft.Data.Entities.Abstractions;

namespace Olbrasoft.VirtualAssistant.Data.Entities;

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

    /// <summary>
    /// Navigation property to associated GitHub issues (many-to-many via junction table).
    /// </summary>
    public ICollection<NotificationGitHubIssue> NotificationGitHubIssues { get; set; } = [];

    // TTS tracking
    public int? FinalProviderId { get; set; }
    public Provider? FinalProvider { get; set; }
    public string? FinalTtsStatus { get; set; } // "success", "error", "timeout", "all_failed"
    public DateTime? TtsCompletedAt { get; set; }
    public ICollection<NotificationTtsAttempt> TtsAttempts { get; set; } = new List<NotificationTtsAttempt>();
}
