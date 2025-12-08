using Olbrasoft.Data.Entities.Abstractions;
using VirtualAssistant.Data.Enums;

namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Represents an agent's response/monolog - one record per agent work session.
/// Simplifies tracking: one monolog = one DB record.
/// Single source of truth for agent status and timing.
/// </summary>
public class AgentResponse : BaseEnity
{
    /// <summary>
    /// Agent identifier (e.g., "opencode", "claude").
    /// </summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the agent response.
    /// </summary>
    public AgentResponseStatus Status { get; set; }

    /// <summary>
    /// When the agent started working.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When the agent completed working (null if still in progress).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Optional link to the task being executed.
    /// Null for responses not tied to a specific task.
    /// </summary>
    public int? AgentTaskId { get; set; }

    /// <summary>
    /// Navigation property to the related task.
    /// </summary>
    public AgentTask? AgentTask { get; set; }
}
