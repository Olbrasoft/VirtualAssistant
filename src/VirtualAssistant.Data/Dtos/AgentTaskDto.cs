namespace VirtualAssistant.Data.Dtos;

/// <summary>
/// Data transfer object for agent task.
/// </summary>
public class AgentTaskDto
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
    /// Name of agent that created this task.
    /// </summary>
    public string? CreatedByAgent { get; set; }

    /// <summary>
    /// Name of agent assigned to execute this task.
    /// </summary>
    public string? TargetAgent { get; set; }

    /// <summary>
    /// Task status: "pending", "approved", "notified", "sent", "completed", "cancelled".
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Whether user must approve before sending.
    /// </summary>
    public bool RequiresApproval { get; set; }

    /// <summary>
    /// Result/outcome when task is completed.
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// When the task was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the user approved the task.
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// When the agent was notified about the task (for pull-based delivery).
    /// </summary>
    public DateTime? NotifiedAt { get; set; }

    /// <summary>
    /// When the task was sent to the target agent.
    /// </summary>
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// When the target agent confirmed receipt and started working.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the task was completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Claude Code session ID from headless mode execution.
    /// </summary>
    public string? ClaudeSessionId { get; set; }
}
