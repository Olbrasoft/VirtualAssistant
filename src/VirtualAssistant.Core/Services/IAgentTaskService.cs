using VirtualAssistant.Data.Dtos;

namespace VirtualAssistant.Core.Services;

/// <summary>
/// Service for inter-agent task queue management.
/// Handles task creation, assignment, and completion between agents.
/// </summary>
public interface IAgentTaskService
{
    /// <summary>
    /// Create a new task for another agent.
    /// </summary>
    /// <param name="sourceAgent">Name of the agent creating the task</param>
    /// <param name="request">Task creation request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created task DTO</returns>
    Task<AgentTaskDto> CreateTaskAsync(string sourceAgent, CreateTaskRequest request, CancellationToken ct = default);

    /// <summary>
    /// Get pending tasks for a specific agent.
    /// </summary>
    /// <param name="agentName">Agent name (e.g., "opencode", "claude")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of pending tasks</returns>
    Task<IReadOnlyList<AgentTaskDto>> GetPendingTasksAsync(string agentName, CancellationToken ct = default);

    /// <summary>
    /// Get tasks awaiting user approval.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of tasks pending approval</returns>
    Task<IReadOnlyList<AgentTaskDto>> GetAwaitingApprovalAsync(CancellationToken ct = default);

    /// <summary>
    /// Approve a task for sending.
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <param name="ct">Cancellation token</param>
    Task ApproveTaskAsync(int taskId, CancellationToken ct = default);

    /// <summary>
    /// Cancel a pending task.
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <param name="ct">Cancellation token</param>
    Task CancelTaskAsync(int taskId, CancellationToken ct = default);

    /// <summary>
    /// Mark a task as completed with a result.
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <param name="result">Result/outcome description</param>
    /// <param name="ct">Cancellation token</param>
    Task CompleteTaskAsync(int taskId, string result, CancellationToken ct = default);

    /// <summary>
    /// Get a task by ID.
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task DTO or null if not found</returns>
    Task<AgentTaskDto?> GetTaskAsync(int taskId, CancellationToken ct = default);

    /// <summary>
    /// Get all tasks (for overview).
    /// </summary>
    /// <param name="limit">Maximum number of tasks to return</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of tasks ordered by creation time (newest first)</returns>
    Task<IReadOnlyList<AgentTaskDto>> GetAllTasksAsync(int limit = 100, CancellationToken ct = default);

    /// <summary>
    /// Check if agent is idle (last message is Complete phase).
    /// </summary>
    /// <param name="agentName">Agent name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if agent is idle</returns>
    Task<bool> IsAgentIdleAsync(string agentName, CancellationToken ct = default);

    /// <summary>
    /// Get tasks ready to be sent (approved and target agent is idle).
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of tasks ready to send</returns>
    Task<IReadOnlyList<AgentTaskDto>> GetReadyToSendAsync(CancellationToken ct = default);

    /// <summary>
    /// Mark task as sent and log the delivery.
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <param name="deliveryMethod">Delivery method (e.g., "hub_api")</param>
    /// <param name="response">Response from delivery</param>
    /// <param name="ct">Cancellation token</param>
    Task MarkSentAsync(int taskId, string deliveryMethod, string? response = null, CancellationToken ct = default);

    /// <summary>
    /// Mark task as notified (agent was notified but hasn't pulled the task yet).
    /// Used for pull-based delivery (e.g., OpenCode).
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <param name="ct">Cancellation token</param>
    Task MarkNotifiedAsync(int taskId, CancellationToken ct = default);

    /// <summary>
    /// Get tasks that have been notified to an agent but not yet pulled.
    /// </summary>
    /// <param name="agentName">Agent name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of notified tasks</returns>
    Task<IReadOnlyList<AgentTaskDto>> GetNotifiedTasksAsync(string agentName, CancellationToken ct = default);

    /// <summary>
    /// Accept a notified task and send it via hub.
    /// Called when agent is ready to receive the task.
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The task prompt content</returns>
    Task<string> AcceptTaskAsync(int taskId, CancellationToken ct = default);

    /// <summary>
    /// Dispatch a task to an agent if they are available.
    /// Checks agent availability, finds pending task, and marks as sent.
    /// </summary>
    /// <param name="targetAgent">Target agent name (e.g., "claude")</param>
    /// <param name="githubIssueNumber">Optional specific issue number</param>
    /// <param name="githubIssueUrl">Optional specific issue URL</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Dispatch result with success status and task details</returns>
    Task<DispatchTaskResult> DispatchTaskAsync(
        string targetAgent,
        int? githubIssueNumber = null,
        string? githubIssueUrl = null,
        CancellationToken ct = default);
}
