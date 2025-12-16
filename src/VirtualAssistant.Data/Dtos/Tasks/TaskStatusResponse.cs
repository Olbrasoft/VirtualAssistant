using System.Text.Json.Serialization;

namespace VirtualAssistant.Data.Dtos.Tasks;

/// <summary>
/// Response model for task-status endpoint.
/// </summary>
public class TaskStatusResponse
{
    /// <summary>
    /// Task ID.
    /// </summary>
    [JsonPropertyName("task_id")]
    public int TaskId { get; set; }

    /// <summary>
    /// GitHub issue number.
    /// </summary>
    [JsonPropertyName("github_issue_number")]
    public int? GithubIssueNumber { get; set; }

    /// <summary>
    /// GitHub issue URL.
    /// </summary>
    [JsonPropertyName("github_issue_url")]
    public string? GithubIssueUrl { get; set; }

    /// <summary>
    /// Task status: pending, sent, completed, failed, blocked.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Task summary/description.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Completion result (filled when completed/failed/blocked).
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// Agent that created this task.
    /// </summary>
    [JsonPropertyName("created_by_agent")]
    public string? CreatedByAgent { get; set; }

    /// <summary>
    /// Target agent for this task.
    /// </summary>
    [JsonPropertyName("target_agent")]
    public string? TargetAgent { get; set; }

    /// <summary>
    /// When the task was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the task was sent to the target agent.
    /// </summary>
    [JsonPropertyName("sent_at")]
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// When the task was completed (if applicable).
    /// </summary>
    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }
}
