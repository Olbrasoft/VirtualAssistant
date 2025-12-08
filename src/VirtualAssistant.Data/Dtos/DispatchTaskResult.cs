namespace VirtualAssistant.Data.Dtos;

/// <summary>
/// Result of dispatching a task to an agent.
/// </summary>
public class DispatchTaskResult
{
    /// <summary>
    /// Whether the dispatch was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Reason for failure (if not successful).
    /// Values: "agent_busy", "no_pending_tasks", "task_not_found"
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Human-readable message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Task ID (if dispatched successfully).
    /// </summary>
    public int? TaskId { get; set; }

    /// <summary>
    /// GitHub issue number (if dispatched successfully).
    /// </summary>
    public int? GithubIssueNumber { get; set; }

    /// <summary>
    /// GitHub issue URL (if dispatched successfully).
    /// </summary>
    public string? GithubIssueUrl { get; set; }

    /// <summary>
    /// Task summary/description (if dispatched successfully).
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Create a success result.
    /// </summary>
    public static DispatchTaskResult Dispatched(int taskId, int? issueNumber, string? issueUrl, string? summary)
    {
        return new DispatchTaskResult
        {
            Success = true,
            TaskId = taskId,
            GithubIssueNumber = issueNumber,
            GithubIssueUrl = issueUrl,
            Summary = summary,
            Message = "Task dispatched to Claude"
        };
    }

    /// <summary>
    /// Create a failure result for busy agent.
    /// </summary>
    public static DispatchTaskResult AgentBusy(string agentName)
    {
        return new DispatchTaskResult
        {
            Success = false,
            Reason = "agent_busy",
            Message = $"{agentName} is currently working on another task"
        };
    }

    /// <summary>
    /// Create a failure result for no pending tasks.
    /// </summary>
    public static DispatchTaskResult NoPendingTasks(string agentName)
    {
        return new DispatchTaskResult
        {
            Success = false,
            Reason = "no_pending_tasks",
            Message = $"No pending tasks for {agentName}"
        };
    }

    /// <summary>
    /// Create a failure result for task not found.
    /// </summary>
    public static DispatchTaskResult TaskNotFound(int issueNumber)
    {
        return new DispatchTaskResult
        {
            Success = false,
            Reason = "task_not_found",
            Message = $"No pending task found for issue #{issueNumber}"
        };
    }
}
