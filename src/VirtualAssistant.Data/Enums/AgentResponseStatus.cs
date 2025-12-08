namespace VirtualAssistant.Data.Enums;

/// <summary>
/// Represents the status of an agent response/monolog.
/// </summary>
public enum AgentResponseStatus
{
    /// <summary>
    /// Agent is currently working on a task.
    /// </summary>
    InProgress,

    /// <summary>
    /// Agent has completed the task.
    /// </summary>
    Completed
}
