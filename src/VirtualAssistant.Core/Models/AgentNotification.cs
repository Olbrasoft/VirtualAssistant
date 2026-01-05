namespace Olbrasoft.VirtualAssistant.Core.Models;

/// <summary>
/// Represents a notification from an agent that will be queued for TTS processing.
/// Used by the notification batching system to track notifications from various agents.
/// </summary>
public class AgentNotification
{
    /// <summary>
    /// Optional database notification ID for status tracking.
    /// </summary>
    public int? NotificationId { get; set; }

    /// <summary>
    /// Source agent identifier (e.g., "claude-code", "opencode").
    /// </summary>
    public required string Agent { get; set; }

    /// <summary>
    /// Notification type (e.g., "status", "alert").
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Notification text content to be spoken.
    /// </summary>
    public required string Content { get; set; }
}
