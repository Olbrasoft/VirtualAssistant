namespace VirtualAssistant.Core.Services;

/// <summary>
/// Service for detecting and managing orphaned tasks after service restart.
/// An orphaned task is one where AgentResponse.Status = InProgress but the agent is actually idle.
/// </summary>
public interface IOrphanedTaskService
{
    /// <summary>
    /// Find all stuck/orphaned agent responses (status=InProgress, completedAt=null).
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of orphaned task information</returns>
    Task<IReadOnlyList<OrphanedTaskInfo>> FindOrphanedTasksAsync(CancellationToken ct = default);

    /// <summary>
    /// Mark an orphaned task as completed (human decision: "it's done").
    /// </summary>
    /// <param name="agentResponseId">AgentResponse ID to complete</param>
    /// <param name="ct">Cancellation token</param>
    Task MarkAsCompletedAsync(int agentResponseId, CancellationToken ct = default);

    /// <summary>
    /// Reset an orphaned task to pending (human decision: "try again").
    /// </summary>
    /// <param name="agentResponseId">AgentResponse ID to reset</param>
    /// <param name="ct">Cancellation token</param>
    Task ResetTaskAsync(int agentResponseId, CancellationToken ct = default);

    /// <summary>
    /// Ignore an orphaned task (human decision: "leave it").
    /// Marks the response as completed without affecting the task.
    /// </summary>
    /// <param name="agentResponseId">AgentResponse ID to ignore</param>
    /// <param name="ct">Cancellation token</param>
    Task IgnoreAsync(int agentResponseId, CancellationToken ct = default);
}

/// <summary>
/// Information about an orphaned task for human review.
/// </summary>
public record OrphanedTaskInfo
{
    /// <summary>
    /// AgentResponse ID.
    /// </summary>
    public int AgentResponseId { get; init; }

    /// <summary>
    /// Agent name (e.g., "claude").
    /// </summary>
    public string AgentName { get; init; } = string.Empty;

    /// <summary>
    /// When the agent started working.
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// Related AgentTask ID (if any).
    /// </summary>
    public int? TaskId { get; init; }

    /// <summary>
    /// GitHub issue number (if related to an issue).
    /// </summary>
    public int? GithubIssueNumber { get; init; }

    /// <summary>
    /// Task summary.
    /// </summary>
    public string? TaskSummary { get; init; }

    /// <summary>
    /// GitHub issue status (open/closed).
    /// </summary>
    public string? GithubIssueStatus { get; init; }

    /// <summary>
    /// Current agent status (idle/working).
    /// </summary>
    public string? AgentStatus { get; init; }
}
