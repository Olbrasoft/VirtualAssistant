namespace VirtualAssistant.Data.Dtos;

/// <summary>
/// Request DTO for creating a task for another agent.
/// </summary>
public class CreateTaskRequest
{
    /// <summary>
    /// Full GitHub issue URL (e.g., "https://github.com/Olbrasoft/VirtualAssistant/issues/177").
    /// </summary>
    public string GithubIssueUrl { get; set; } = string.Empty;

    /// <summary>
    /// Brief description of the task.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Target agent name (e.g., "claude", "opencode").
    /// </summary>
    public string TargetAgent { get; set; } = string.Empty;

    /// <summary>
    /// Whether user must approve before sending. Defaults to true.
    /// </summary>
    public bool RequiresApproval { get; set; } = true;
}
