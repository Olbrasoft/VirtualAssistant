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
    public AgentHubController(IAgentHubService hubService, ILogger<AgentHubController> logger)
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

    /// <summary>
    /// Start a new task.
    /// </summary>
    /// <param name="request">Task start request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>ID of the created task message</returns>
    /// <response code="201">Task started successfully</response>
    /// <response code="400">Invalid request data</response>
    [HttpPost("start")]
    [ProducesResponseType(typeof(StartTaskResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StartTaskResponse>> StartTask(
        [FromBody] StartTaskRequest request,
        CancellationToken ct)
    {
        try
        {
            var id = await _hubService.StartTaskAsync(request.SourceAgent, request.Content, request.TargetAgent, ct);
            _logger.LogInformation("Task {Id} started by {Source}", id, request.SourceAgent);

            return CreatedAtAction(
                nameof(GetTaskHistory),
                new { taskId = id },
                new StartTaskResponse { MessageId = id });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid task start request");
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
    }

    /// <summary>
    /// Send a progress update for an ongoing task.
    /// </summary>
    /// <param name="request">Progress update request</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="204">Progress update sent</response>
    /// <response code="400">Invalid request or task state</response>
    /// <response code="404">Parent task not found</response>
    [HttpPost("progress")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SendProgress(
        [FromBody] ProgressRequest request,
        CancellationToken ct)
    {
        try
        {
            await _hubService.SendProgressAsync(request.ParentMessageId, request.Content, ct);
            _logger.LogInformation("Progress sent for task {TaskId}", request.ParentMessageId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Parent task {TaskId} not found", request.ParentMessageId);
            return NotFound(new ErrorResponse { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot send progress for task {TaskId}", request.ParentMessageId);
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid progress request");
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
    }

    /// <summary>
    /// Complete a task with a summary.
    /// </summary>
    /// <param name="request">Task completion request</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="204">Task completed</response>
    /// <response code="400">Invalid request or task state</response>
    /// <response code="404">Parent task not found</response>
    [HttpPost("complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> CompleteTask(
        [FromBody] CompleteTaskRequest request,
        CancellationToken ct)
    {
        try
        {
            await _hubService.CompleteTaskAsync(request.ParentMessageId, request.Content, ct);
            _logger.LogInformation("Task {TaskId} completed", request.ParentMessageId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Parent task {TaskId} not found", request.ParentMessageId);
            return NotFound(new ErrorResponse { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot complete task {TaskId}", request.ParentMessageId);
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid complete request");
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
    }

    /// <summary>
    /// Get currently active tasks.
    /// </summary>
    /// <param name="agent">Optional filter by source agent</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of active tasks</returns>
    /// <response code="200">List of active tasks</response>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IEnumerable<AgentMessageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AgentMessageDto>>> GetActiveTasks(
        [FromQuery] string? agent,
        CancellationToken ct)
    {
        var tasks = await _hubService.GetActiveTasksAsync(agent, ct);
        _logger.LogDebug("Retrieved {Count} active tasks for {Agent}", tasks.Count, agent ?? "all");
        return Ok(tasks);
    }

    /// <summary>
    /// Get full task history including all progress and completion messages.
    /// </summary>
    /// <param name="taskId">ID of the start message</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>All messages related to this task</returns>
    /// <response code="200">Task history</response>
    /// <response code="404">Task not found</response>
    [HttpGet("task/{taskId:int}")]
    [ProducesResponseType(typeof(IEnumerable<AgentMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<AgentMessageDto>>> GetTaskHistory(
        int taskId,
        CancellationToken ct)
    {
        try
        {
            var history = await _hubService.GetTaskHistoryAsync(taskId, ct);
            _logger.LogDebug("Retrieved {Count} messages for task {TaskId}", history.Count, taskId);
            return Ok(history);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Task {TaskId} not found", taskId);
            return NotFound(new ErrorResponse { Error = ex.Message });
        }
    }
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
