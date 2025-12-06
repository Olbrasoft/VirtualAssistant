namespace VirtualAssistant.Data.Enums;

/// <summary>
/// Represents the phase of an agent message in a task workflow.
/// </summary>
public enum MessagePhase
{
    /// <summary>
    /// Agent is starting work on a task.
    /// </summary>
    Start,

    /// <summary>
    /// Progress update during task execution.
    /// </summary>
    Progress,

    /// <summary>
    /// Task is complete with final summary.
    /// </summary>
    Complete
}
