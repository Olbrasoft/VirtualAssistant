namespace VirtualAssistant.Data.Dtos.Tasks;

/// <summary>
/// Information about a fetched task.
/// </summary>
public class FetchedTaskInfo
{
    /// <summary>
    /// Task ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Agent that created this task.
    /// </summary>
    public string FromAgent { get; set; } = string.Empty;

    /// <summary>
    /// Task content/prompt.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// When the task was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// GitHub issue reference (e.g., "#184").
    /// </summary>
    public string? GithubIssue { get; set; }
}
