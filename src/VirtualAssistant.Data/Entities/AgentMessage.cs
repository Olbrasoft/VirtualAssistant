using Olbrasoft.Data.Entities.Abstractions;

namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Represents a message in the inter-agent communication hub.
/// Messages flow between AI agents (OpenCode, Claude Code) via VirtualAssistant.
/// </summary>
public class AgentMessage : BaseEnity
{
    /// <summary>
    /// Source agent identifier (e.g., "opencode", "claude", "user")
    /// </summary>
    public string SourceAgent { get; set; } = string.Empty;

    /// <summary>
    /// Target agent identifier (e.g., "opencode", "claude", "user")
    /// </summary>
    public string TargetAgent { get; set; } = string.Empty;

    /// <summary>
    /// Message type: "task", "completion", "review_result", "question"
    /// </summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// The actual message content (task description, completion summary, etc.)
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Optional JSON metadata (issue number, repository, priority, etc.)
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Message status: "pending", "approved", "delivered", "processed", "cancelled"
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Whether this message requires user approval before delivery
    /// </summary>
    public bool RequiresApproval { get; set; }

    /// <summary>
    /// When the message was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the user approved the message (if RequiresApproval was true)
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// When the message was delivered to the target agent
    /// </summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>
    /// When the target agent confirmed processing
    /// </summary>
    public DateTime? ProcessedAt { get; set; }
}
