using Olbrasoft.Data.Entities.Abstractions;

namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Represents a task in the inter-agent task queue.
/// Tasks are created by one agent for another to execute.
/// </summary>
public class AgentTask : BaseEnity
{
    /// <summary>
    /// Full URL to the GitHub issue (e.g., "https://github.com/Olbrasoft/VirtualAssistant/issues/177").
    /// </summary>
    public string GithubIssueUrl { get; set; } = string.Empty;

    /// <summary>
    /// Extracted GitHub issue number (e.g., 177).
    /// </summary>
    public int? GithubIssueNumber { get; set; }

    /// <summary>
    /// Brief description of the task (the task text).
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// ID of the agent that created this task.
    /// </summary>
    public int? CreatedByAgentId { get; set; }

    /// <summary>
    /// Agent that created this task.
    /// </summary>
    public Agent? CreatedByAgent { get; set; }

    /// <summary>
    /// ID of the agent assigned to execute this task.
    /// </summary>
    public int? TargetAgentId { get; set; }

    /// <summary>
    /// Agent assigned to execute this task.
    /// </summary>
    public Agent? TargetAgent { get; set; }

    /// <summary>
    /// Task status: "pending", "approved", "notified", "sent", "completed", "cancelled".
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Whether user must approve before sending to target agent.
    /// </summary>
    public bool RequiresApproval { get; set; } = true;

    /// <summary>
    /// Result/outcome description when task is completed.
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// When the task was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the user approved the task (if RequiresApproval was true).
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
    /// When the task was completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Claude Code session ID from headless mode execution.
    /// Used for potential multi-turn follow-up.
    /// </summary>
    public string? ClaudeSessionId { get; set; }

    /// <summary>
    /// Delivery logs for this task.
    /// </summary>
    public ICollection<AgentTaskSend> Sends { get; set; } = new List<AgentTaskSend>();
}
