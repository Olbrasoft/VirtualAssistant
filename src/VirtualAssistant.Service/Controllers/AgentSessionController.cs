using Microsoft.AspNetCore.Mvc;
using VirtualAssistant.Core.Services;
using VirtualAssistant.Data.Dtos;

namespace Olbrasoft.VirtualAssistant.Service.Controllers;

/// <summary>
/// REST API controller for agent session management.
/// Provides endpoints for starting, tracking progress, and completing agent sessions/tasks.
/// </summary>
[ApiController]
[Route("api/hub")]
[Produces("application/json")]
public class AgentSessionController : ControllerBase
{
    private readonly IAgentHubService _hubService;
    private readonly ILogger<AgentSessionController> _logger;

    /// <summary>
    /// Initializes a new instance of the AgentSessionController.
    /// </summary>
    /// <param name="hubService">The agent hub service</param>
    /// <param name="logger">The logger</param>
    public AgentSessionController(
        IAgentHubService hubService,
        ILogger<AgentSessionController> logger)
    {
        _hubService = hubService;
        _logger = logger;
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
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StartTaskResponse>> StartTask(
        [FromBody] StartTaskRequest request,
        CancellationToken ct)
    {
        try
        {
            var id = await _hubService.StartTaskAsync(
                request.SourceAgent,
                request.Content,
                request.TargetAgent,
                request.SessionId,
                ct);

            _logger.LogInformation("Task {Id} started by {Source}, session: {Session}",
                id, request.SourceAgent, request.SessionId ?? "(none)");

            return CreatedAtAction(
                nameof(GetTaskHistory),
                new { taskId = id },
                new StartTaskResponse { MessageId = id });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already has a Start"))
        {
            _logger.LogWarning(ex, "Duplicate Start rejected for session {Session}", request.SessionId);
            return Conflict(new ErrorResponse { Error = ex.Message });
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
