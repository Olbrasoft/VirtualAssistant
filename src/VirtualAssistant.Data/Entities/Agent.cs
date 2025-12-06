using Olbrasoft.Data.Entities.Abstractions;

namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Represents a registered agent worker in the system.
/// Agents are AI workers like OpenCode, Claude Code, etc.
/// </summary>
public class Agent : BaseEnity
{
    /// <summary>
    /// Unique agent identifier (e.g., "opencode", "claude").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// GitHub label matching this agent (e.g., "agent:opencode", "agent:claude").
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Whether the agent is currently active and can receive tasks.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When the agent was registered.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Tasks created by this agent.
    /// </summary>
    public ICollection<AgentTask> CreatedTasks { get; set; } = new List<AgentTask>();

    /// <summary>
    /// Tasks assigned to this agent.
    /// </summary>
    public ICollection<AgentTask> AssignedTasks { get; set; } = new List<AgentTask>();

    /// <summary>
    /// Task delivery logs for this agent.
    /// </summary>
    public ICollection<AgentTaskSend> TaskSends { get; set; } = new List<AgentTaskSend>();
}
