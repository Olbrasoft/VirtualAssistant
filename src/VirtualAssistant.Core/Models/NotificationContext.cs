namespace Olbrasoft.VirtualAssistant.Core.Models;

/// <summary>
/// Represents the context of a notification, including target application and urgency.
/// </summary>
public record NotificationContext(
    string? TargetApplication,
    bool IsUrgent,
    NotificationSource Source
);

/// <summary>
/// Source type of a notification.
/// </summary>
public enum NotificationSource
{
    /// <summary>
    /// Notification from task completion (e.g., Claude Code finished task).
    /// </summary>
    TaskCompletion,

    /// <summary>
    /// Notification from GitHub event (e.g., issue created, PR merged).
    /// </summary>
    GitHubEvent,

    /// <summary>
    /// System alert or critical notification.
    /// </summary>
    SystemAlert,

    /// <summary>
    /// Direct user message or general notification.
    /// </summary>
    UserMessage
}
