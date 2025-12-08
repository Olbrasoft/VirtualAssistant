using Microsoft.AspNetCore.Mvc;
using VirtualAssistant.Core.Services;
using VirtualAssistant.Data.Dtos;

namespace Olbrasoft.VirtualAssistant.Service.Controllers;

/// <summary>
/// REST API controller for inter-agent communication hub.
/// Provides endpoints for sending, receiving, and managing messages between AI agents.
/// </summary>
[ApiController]
[Route("api/hub")]
[Produces("application/json")]
public class AgentHubController : ControllerBase
{
    private readonly IAgentHubService _hubService;
    private readonly ILogger<AgentHubController> _logger;

    /// <summary>
    /// Initializes a new instance of the AgentHubController.
    /// </summary>
    /// <param name="hubService">The agent hub service</param>
    /// <param name="logger">The logger</param>
    public AgentHubController(
        IAgentHubService hubService,
        ILogger<AgentHubController> logger)
    {
        _hubService = hubService;
        _logger = logger;
    }

    /// <summary>
    /// Send a message to another agent.
    /// </summary>
    /// <param name="message">The message to send</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The ID of the created message</returns>
    /// <response code="201">Message created successfully</response>
    /// <response code="400">Invalid message data</response>
    [HttpPost("send")]
    [ProducesResponseType(typeof(SendMessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SendMessageResponse>> Send(
        [FromBody] AgentMessageDto message,
        CancellationToken ct)
    {
        try
        {
            var id = await _hubService.SendAsync(message, ct);
            _logger.LogInformation("Message {Id} sent from {Source} to {Target}",
                id, message.SourceAgent, message.TargetAgent);

            return CreatedAtAction(
                nameof(GetQueue),
                new SendMessageResponse { Id = id, Status = "pending" });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid message data");
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
    }

    /// <summary>
    /// Get pending messages for a specific agent.
    /// </summary>
    /// <param name="agent">Agent identifier (e.g., "opencode", "claude")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of pending messages</returns>
    /// <response code="200">List of pending messages</response>
    /// <response code="400">Invalid agent identifier</response>
    [HttpGet("pending/{agent}")]
    [ProducesResponseType(typeof(IEnumerable<AgentMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<AgentMessageDto>>> GetPending(
        string agent,
        CancellationToken ct)
    {
        try
        {
            var messages = await _hubService.GetPendingAsync(agent, ct);
            _logger.LogDebug("Retrieved {Count} pending messages for {Agent}", messages.Count, agent);
            return Ok(messages);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid agent identifier: {Agent}", agent);
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
    }

    /// <summary>
    /// Approve a message that requires user approval.
    /// </summary>
    /// <param name="id">Message ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="204">Message approved successfully</response>
    /// <response code="400">Cannot approve message (invalid state)</response>
    /// <response code="404">Message not found</response>
    [HttpPost("approve/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Approve(int id, CancellationToken ct)
    {
        try
        {
            await _hubService.ApproveAsync(id, ct);
            _logger.LogInformation("Message {Id} approved", id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Message {Id} not found", id);
            return NotFound(new ErrorResponse { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot approve message {Id}", id);
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
    }

    /// <summary>
    /// Cancel a pending message.
    /// </summary>
    /// <param name="id">Message ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="204">Message cancelled successfully</response>
    /// <response code="400">Cannot cancel message (invalid state)</response>
    /// <response code="404">Message not found</response>
    [HttpPost("cancel/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Cancel(int id, CancellationToken ct)
    {
        try
        {
            await _hubService.CancelAsync(id, ct);
            _logger.LogInformation("Message {Id} cancelled", id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Message {Id} not found", id);
            return NotFound(new ErrorResponse { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot cancel message {Id}", id);
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
    }

    /// <summary>
    /// Mark message as delivered to target agent.
    /// </summary>
    /// <param name="id">Message ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="204">Message marked as delivered</response>
    /// <response code="404">Message not found</response>
    [HttpPost("delivered/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> MarkDelivered(int id, CancellationToken ct)
    {
        try
        {
            await _hubService.MarkDeliveredAsync(id, ct);
            _logger.LogInformation("Message {Id} marked as delivered", id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Message {Id} not found", id);
            return NotFound(new ErrorResponse { Error = ex.Message });
        }
    }

    /// <summary>
    /// Mark message as processed by target agent.
    /// </summary>
    /// <param name="id">Message ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="204">Message marked as processed</response>
    /// <response code="404">Message not found</response>
    [HttpPost("processed/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> MarkProcessed(int id, CancellationToken ct)
    {
        try
        {
            await _hubService.MarkProcessedAsync(id, ct);
            _logger.LogInformation("Message {Id} marked as processed", id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Message {Id} not found", id);
            return NotFound(new ErrorResponse { Error = ex.Message });
        }
    }

    /// <summary>
    /// Get all messages in queue (for user overview).
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>All messages ordered by creation time (newest first)</returns>
    /// <response code="200">List of all messages</response>
    [HttpGet("queue")]
    [ProducesResponseType(typeof(IEnumerable<AgentMessageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AgentMessageDto>>> GetQueue(CancellationToken ct)
    {
        var messages = await _hubService.GetQueueAsync(ct);
        _logger.LogDebug("Retrieved {Count} messages from queue", messages.Count);
        return Ok(messages);
    }

    /// <summary>
    /// Get messages awaiting user approval.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Messages with RequiresApproval=true and Status=pending</returns>
    /// <response code="200">List of messages awaiting approval</response>
    [HttpGet("awaiting-approval")]
    [ProducesResponseType(typeof(IEnumerable<AgentMessageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AgentMessageDto>>> GetAwaitingApproval(CancellationToken ct)
    {
        var messages = await _hubService.GetAwaitingApprovalAsync(ct);
        _logger.LogDebug("Retrieved {Count} messages awaiting approval", messages.Count);
        return Ok(messages);
    }
}

#region DTOs

/// <summary>
/// Request model for send task endpoint.
/// </summary>
public class SendTaskRequest
{
    /// <summary>
    /// Source agent name (who is sending the task).
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("source_agent")]
    public string SourceAgent { get; set; } = string.Empty;

    /// <summary>
    /// Target agent name (who should receive the task).
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("target_agent")]
    public string TargetAgent { get; set; } = string.Empty;

    /// <summary>
    /// Task content/description.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Optional GitHub issue reference (e.g., "#123" or "123").
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("github_issue")]
    public string? GithubIssue { get; set; }

    /// <summary>
    /// Optional priority: "normal" (default) or "high".
    /// High priority tasks skip approval.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("priority")]
    public string? Priority { get; set; }
}

/// <summary>
/// Response model for send task endpoint.
/// </summary>
public class SendTaskResponse
{
    /// <summary>
    /// Whether the task was queued successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// ID of the created task.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("task_id")]
    public int TaskId { get; set; }

    /// <summary>
    /// Confirmation message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Response model for fetch task endpoint.
/// </summary>
public class FetchTaskResponse
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The fetched task, or null if no tasks available.
    /// </summary>
    public FetchedTaskInfo? Task { get; set; }

    /// <summary>
    /// Optional message (e.g., "No pending tasks").
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// Information about a fetched task.
/// </summary>
public class FetchedTaskInfo
{
    /// <summary>
    /// Task ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Agent that created this task.
    /// </summary>
    public string FromAgent { get; set; } = string.Empty;

    /// <summary>
    /// Task content/prompt.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// When the task was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// GitHub issue reference (e.g., "#184").
    /// </summary>
    public string? GithubIssue { get; set; }
}

/// <summary>
/// Request model for clipboard operation.
/// </summary>
public class ClipboardRequest
{
    /// <summary>
    /// Content to copy to clipboard.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Response model for clipboard operation.
/// </summary>
public class ClipboardResponse
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Status message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Request model for starting a new task.
/// </summary>
public class StartTaskRequest
{
    /// <summary>
    /// Source agent identifier (e.g., "opencode", "claude").
    /// </summary>
    public string SourceAgent { get; set; } = string.Empty;

    /// <summary>
    /// Task description/content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Optional target agent.
    /// </summary>
    public string? TargetAgent { get; set; }

    /// <summary>
    /// Optional session identifier for tracking related messages.
    /// Only one Start is allowed per session.
    /// </summary>
    public string? SessionId { get; set; }
}

/// <summary>
/// Response model for start task endpoint.
/// </summary>
public class StartTaskResponse
{
    /// <summary>
    /// ID of the created task message.
    /// </summary>
    public int MessageId { get; set; }
}

/// <summary>
/// Request model for progress update.
/// </summary>
public class ProgressRequest
{
    /// <summary>
    /// ID of the parent (start) message.
    /// </summary>
    public int ParentMessageId { get; set; }

    /// <summary>
    /// Progress update content.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Request model for task completion.
/// </summary>
public class CompleteTaskRequest
{
    /// <summary>
    /// ID of the parent (start) message.
    /// </summary>
    public int ParentMessageId { get; set; }

    /// <summary>
    /// Completion summary.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Response model for send message endpoint.
/// </summary>
public class SendMessageResponse
{
    /// <summary>
    /// ID of the created message.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Current status of the message.
    /// </summary>
    public string Status { get; set; } = "pending";
}

/// <summary>
/// Error response model.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Error message.
    /// </summary>
    public string Error { get; set; } = string.Empty;
}

/// <summary>
/// Request model for dispatch task endpoint.
/// </summary>
public class DispatchTaskRequest
{
    /// <summary>
    /// Target agent (default: "claude").
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("target_agent")]
    public string? TargetAgent { get; set; }

    /// <summary>
    /// Optional specific GitHub issue number.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("github_issue_number")]
    public int? GithubIssueNumber { get; set; }

    /// <summary>
    /// Optional specific GitHub issue URL.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("github_issue_url")]
    public string? GithubIssueUrl { get; set; }
}

/// <summary>
/// Response model for dispatch task endpoint.
/// </summary>
public class DispatchTaskResponse
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
    [System.Text.Json.Serialization.JsonPropertyName("task_id")]
    public int? TaskId { get; set; }

    /// <summary>
    /// GitHub issue number (if dispatched successfully).
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("github_issue_number")]
    public int? GithubIssueNumber { get; set; }

    /// <summary>
    /// GitHub issue URL (if dispatched successfully).
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("github_issue_url")]
    public string? GithubIssueUrl { get; set; }

    /// <summary>
    /// Task summary/description (if dispatched successfully).
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Claude Code session ID (if Claude was invoked).
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("claude_session_id")]
    public string? ClaudeSessionId { get; set; }
}

/// <summary>
/// Request model for complete-task endpoint (called by Claude).
/// </summary>
public class CompleteTaskApiRequest
{
    /// <summary>
    /// Task ID to complete.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("task_id")]
    public int? TaskId { get; set; }

    /// <summary>
    /// Alternative: GitHub issue number.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("github_issue_number")]
    public int? GithubIssueNumber { get; set; }

    /// <summary>
    /// Result/outcome description.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("result")]
    public string? Result { get; set; }

    /// <summary>
    /// Status: "completed", "failed", or "blocked".
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Whether to automatically dispatch the next pending task after completion.
    /// Default: true.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("auto_dispatch")]
    public bool? AutoDispatch { get; set; }
}

/// <summary>
/// Response model for complete-task endpoint.
/// </summary>
public class CompleteTaskApiResponse
{
    /// <summary>
    /// Whether the completion was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Task ID that was completed.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("task_id")]
    public int TaskId { get; set; }

    /// <summary>
    /// Confirmation message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Information about the next task that was auto-dispatched (if any).
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("next_task")]
    public NextTaskInfo? NextTask { get; set; }
}

/// <summary>
/// Information about an auto-dispatched task.
/// </summary>
public class NextTaskInfo
{
    /// <summary>
    /// Task ID of the next task.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("task_id")]
    public int TaskId { get; set; }

    /// <summary>
    /// GitHub issue number (if associated).
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("github_issue_number")]
    public int? GithubIssueNumber { get; set; }

    /// <summary>
    /// Task summary/description.
    /// </summary>
    public string? Summary { get; set; }
}

/// <summary>
/// Response model for task-status endpoint.
/// </summary>
public class TaskStatusResponse
{
    /// <summary>
    /// Task ID.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("task_id")]
    public int TaskId { get; set; }

    /// <summary>
    /// GitHub issue number.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("github_issue_number")]
    public int? GithubIssueNumber { get; set; }

    /// <summary>
    /// GitHub issue URL.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("github_issue_url")]
    public string? GithubIssueUrl { get; set; }

    /// <summary>
    /// Task status: pending, sent, completed, failed, blocked.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Task summary/description.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Completion result (filled when completed/failed/blocked).
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// Agent that created this task.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("created_by_agent")]
    public string? CreatedByAgent { get; set; }

    /// <summary>
    /// Target agent for this task.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("target_agent")]
    public string? TargetAgent { get; set; }

    /// <summary>
    /// When the task was created.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the task was sent to target agent.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("sent_at")]
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// When the task was completed.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }
}

#endregion
