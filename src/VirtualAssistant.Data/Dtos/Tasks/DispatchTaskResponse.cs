using System.Text.Json.Serialization;

namespace VirtualAssistant.Data.Dtos.Tasks;

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
