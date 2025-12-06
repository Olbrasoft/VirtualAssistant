namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Represents the many-to-many relationship between GitHub issues and agents.
/// An issue can be assigned to multiple agents (claude, opencode, user).
/// </summary>
public class GitHubIssueAgent
{
    /// <summary>
    /// Gets or sets the foreign key to the GitHub issue.
    /// </summary>
    public int GitHubIssueId { get; set; }

    /// <summary>
    /// Gets or sets the agent name (e.g., "claude", "opencode", "user").
    /// </summary>
    public string Agent { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the navigation property to the GitHub issue.
    /// </summary>
    public GitHubIssue GitHubIssue { get; set; } = null!;
}
