using System.Text.Json.Serialization;

namespace VirtualAssistant.Data.Dtos;

/// <summary>
/// Error response model.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Error message.
    /// </summary>
    public string Error { get; set; } = string.Empty;
}

/// <summary>
/// Request model for clipboard operation.
/// </summary>
public class ClipboardRequest
{
    /// <summary>
    /// Content to copy to clipboard.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Response model for clipboard operation.
/// </summary>
public class ClipboardResponse
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Status message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Response model for fetch task endpoint.
/// </summary>
public class FetchTaskResponse
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The fetched task, or null if no tasks available.
    /// </summary>
    public FetchedTaskInfo? Task { get; set; }

    /// <summary>
    /// Optional message (e.g., "No pending tasks").
    /// </summary>
    public string? Message { get; set; }
}

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

/// <summary>
/// Request model for dispatch task endpoint.
/// </summary>
public class DispatchTaskRequest
{
    /// <summary>
    /// Target agent (default: "claude").
    /// </summary>
    [JsonPropertyName("target_agent")]
    public string? TargetAgent { get; set; }

    /// <summary>
    /// Optional specific GitHub issue number.
    /// </summary>
    [JsonPropertyName("github_issue_number")]
    public int? GithubIssueNumber { get; set; }

    /// <summary>
    /// Optional specific GitHub issue URL.
    /// </summary>
    [JsonPropertyName("github_issue_url")]
    public string? GithubIssueUrl { get; set; }
}

/// <summary>
/// Response model for dispatch task endpoint.
/// </summary>
public class DispatchTaskResponse
{
    /// <summary>
    /// Whether the dispatch was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Reason for failure (if not successful).
    /// Values: "agent_busy", "no_pending_tasks", "task_not_found"
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Human-readable message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Task ID (if dispatched successfully).
    /// </summary>
    [JsonPropertyName("task_id")]
    public int? TaskId { get; set; }

    /// <summary>
    /// GitHub issue number (if dispatched successfully).
    /// </summary>
    [JsonPropertyName("github_issue_number")]
    public int? GithubIssueNumber { get; set; }

    /// <summary>
    /// GitHub issue URL (if dispatched successfully).
    /// </summary>
    [JsonPropertyName("github_issue_url")]
    public string? GithubIssueUrl { get; set; }

    /// <summary>
    /// Task summary/description (if dispatched successfully).
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Claude session ID from headless execution (if applicable).
    /// </summary>
    [JsonPropertyName("claude_session_id")]
    public string? ClaudeSessionId { get; set; }
}

/// <summary>
/// Request model for complete-task endpoint.
/// </summary>
public class CompleteTaskApiRequest
{
    /// <summary>
    /// Task ID to complete.
    /// </summary>
    [JsonPropertyName("task_id")]
    public int? TaskId { get; set; }

    /// <summary>
    /// Alternative: GitHub issue number.
    /// </summary>
    [JsonPropertyName("github_issue_number")]
    public int? GithubIssueNumber { get; set; }

    /// <summary>
    /// Result/outcome description.
    /// </summary>
    [JsonPropertyName("result")]
    public string? Result { get; set; }

    /// <summary>
    /// Status: "completed", "failed", or "blocked".
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Whether to automatically dispatch the next pending task after completion.
    /// Default: true.
    /// </summary>
    [JsonPropertyName("auto_dispatch")]
    public bool? AutoDispatch { get; set; }
}

/// <summary>
/// Response model for complete-task endpoint.
/// </summary>
public class CompleteTaskApiResponse
{
    /// <summary>
    /// Whether the completion was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Task ID that was completed.
    /// </summary>
    [JsonPropertyName("task_id")]
    public int TaskId { get; set; }

    /// <summary>
    /// Confirmation message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Information about the next task that was auto-dispatched (if any).
    /// </summary>
    [JsonPropertyName("next_task")]
    public NextTaskInfo? NextTask { get; set; }
}

/// <summary>
/// Information about an auto-dispatched task.
/// </summary>
public class NextTaskInfo
{
    /// <summary>
    /// Task ID of the next task.
    /// </summary>
    [JsonPropertyName("task_id")]
    public int TaskId { get; set; }

    /// <summary>
    /// GitHub issue number (if associated).
    /// </summary>
    [JsonPropertyName("github_issue_number")]
    public int? GithubIssueNumber { get; set; }

    /// <summary>
    /// Task summary/description.
    /// </summary>
    public string? Summary { get; set; }
}

/// <summary>
/// Request model for send task endpoint.
/// </summary>
public class SendTaskRequest
{
    /// <summary>
    /// Source agent name (who is sending the task).
    /// </summary>
    [JsonPropertyName("source_agent")]
    public string SourceAgent { get; set; } = string.Empty;

    /// <summary>
    /// Target agent name (who should receive the task).
    /// </summary>
    [JsonPropertyName("target_agent")]
    public string TargetAgent { get; set; } = string.Empty;

    /// <summary>
    /// Task content/description.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Optional GitHub issue reference (e.g., "#123" or "123").
    /// </summary>
    [JsonPropertyName("github_issue")]
    public string? GithubIssue { get; set; }

    /// <summary>
    /// Optional priority: "normal" (default) or "high".
    /// High priority tasks skip approval.
    /// </summary>
    [JsonPropertyName("priority")]
    public string? Priority { get; set; }
}

/// <summary>
/// Response model for send task endpoint.
/// </summary>
public class SendTaskResponse
{
    /// <summary>
    /// Whether the task was queued successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// ID of the created task.
    /// </summary>
    [JsonPropertyName("task_id")]
    public int TaskId { get; set; }

    /// <summary>
    /// Confirmation message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

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
