using VirtualAssistant.Data.Enums;

namespace VirtualAssistant.Data.Dtos;

/// <summary>
/// Data transfer object for agent messages.
/// </summary>
public class AgentMessageDto
{
    public int? Id { get; set; }
    public string SourceAgent { get; set; } = string.Empty;
    public string TargetAgent { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, object>? Metadata { get; set; }
    public bool RequiresApproval { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime? CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Phase of the message in a task workflow.
    /// </summary>
    public MessagePhase Phase { get; set; } = MessagePhase.Complete;

    /// <summary>
    /// Reference to parent message ID (for Progress/Complete messages).
    /// </summary>
    public int? ParentMessageId { get; set; }
}
