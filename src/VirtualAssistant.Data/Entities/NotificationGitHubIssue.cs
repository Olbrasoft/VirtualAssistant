namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Junction entity for many-to-many relationship between Notification and GitHub issues.
/// Stores the GitHub issue ID associated with a notification.
/// </summary>
public class NotificationGitHubIssue
{
    /// <summary>
    /// Foreign key to Notification.
    /// </summary>
    public int NotificationId { get; set; }

    /// <summary>
    /// GitHub issue ID (from GitHub.Issues database or GitHub API).
    /// </summary>
    public int GitHubIssueId { get; set; }

    /// <summary>
    /// Navigation property to the notification.
    /// </summary>
    public Notification Notification { get; set; } = null!;
}
