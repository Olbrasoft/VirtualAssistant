namespace VirtualAssistant.Data.Dtos;

/// <summary>
/// Response model for GET /api/claude/tasks/pending endpoint.
/// Returns the oldest pending task for the Claude agent.
/// </summary>
public class ClaudePendingTaskResponse
{
    /// <summary>
    /// Task ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Full GitHub issue URL.
    /// </summary>
    public string GithubIssueUrl { get; set; } = string.Empty;

    /// <summary>
    /// Extracted GitHub issue number.
    /// </summary>
    public int? GithubIssueNumber { get; set; }

    /// <summary>
    /// Brief description of the task.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// When the task was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
