using Olbrasoft.Data.Entities.Abstractions;

namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Represents a delivery log entry for a task.
/// Records when and how a task was sent to an agent.
/// </summary>
public class AgentTaskSend : BaseEnity
{
    /// <summary>
    /// ID of the task that was sent.
    /// </summary>
    public int TaskId { get; set; }

    /// <summary>
    /// The task that was sent.
    /// </summary>
    public AgentTask Task { get; set; } = null!;

    /// <summary>
    /// ID of the agent the task was sent to.
    /// </summary>
    public int AgentId { get; set; }

    /// <summary>
    /// The agent the task was sent to.
    /// </summary>
    public Agent Agent { get; set; } = null!;

    /// <summary>
    /// When the task was sent.
    /// </summary>
    public DateTime SentAt { get; set; }

    /// <summary>
    /// How the task was delivered (e.g., "hub_api", "notify").
    /// </summary>
    public string? DeliveryMethod { get; set; }

    /// <summary>
    /// Response or result from the delivery attempt.
    /// </summary>
    public string? Response { get; set; }
}
