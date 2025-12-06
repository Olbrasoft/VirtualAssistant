using Microsoft.AspNetCore.Mvc;
using VirtualAssistant.Core.Services;
using VirtualAssistant.Data.Dtos;

namespace Olbrasoft.VirtualAssistant.Service.Controllers;

/// <summary>
/// REST API controller for inter-agent task queue.
/// Provides endpoints for creating, managing, and completing tasks between agents.
/// </summary>
[ApiController]
[Route("api/tasks")]
[Produces("application/json")]
public class TaskQueueController : ControllerBase
{
    private readonly IAgentTaskService _taskService;
    private readonly ILogger<TaskQueueController> _logger;

    public TaskQueueController(IAgentTaskService taskService, ILogger<TaskQueueController> logger)
    {
        _taskService = taskService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new task for another agent.
    /// </summary>
    /// <param name="sourceAgent">Name of the agent creating the task (from header)</param>
    /// <param name="request">Task creation request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created task</returns>
    [HttpPost("create")]
    [ProducesResponseType(typeof(AgentTaskDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AgentTaskDto>> CreateTask(
        [FromHeader(Name = "X-Agent-Name")] string sourceAgent,
        [FromBody] CreateTaskRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sourceAgent))
        {
            return BadRequest(new ErrorResponse { Error = "X-Agent-Name header is required" });
        }

        try
        {
            var task = await _taskService.CreateTaskAsync(sourceAgent, request, ct);

            _logger.LogInformation(
                "Task {Id} created by {Source} for {Target}",
                task.Id, sourceAgent, request.TargetAgent);

            return CreatedAtAction(nameof(GetTask), new { taskId = task.Id }, task);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create task");
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid task request");
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
    }

    /// <summary>
    /// Get pending tasks for a specific agent.
    /// </summary>
    /// <param name="agent">Agent name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of pending tasks</returns>
    [HttpGet("pending/{agent}")]
    [ProducesResponseType(typeof(IEnumerable<AgentTaskDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AgentTaskDto>>> GetPendingTasks(string agent, CancellationToken ct)
    {
        var tasks = await _taskService.GetPendingTasksAsync(agent, ct);
        return Ok(tasks);
    }

    /// <summary>
    /// Get tasks awaiting user approval.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of tasks pending approval</returns>
    [HttpGet("awaiting-approval")]
    [ProducesResponseType(typeof(IEnumerable<AgentTaskDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AgentTaskDto>>> GetAwaitingApproval(CancellationToken ct)
    {
        var tasks = await _taskService.GetAwaitingApprovalAsync(ct);
        return Ok(tasks);
    }

    /// <summary>
    /// Get a specific task by ID.
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task details</returns>
    [HttpGet("{taskId:int}")]
    [ProducesResponseType(typeof(AgentTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgentTaskDto>> GetTask(int taskId, CancellationToken ct)
    {
        var task = await _taskService.GetTaskAsync(taskId, ct);
        if (task == null)
        {
            return NotFound(new ErrorResponse { Error = $"Task {taskId} not found" });
        }
        return Ok(task);
    }

    /// <summary>
    /// Get all tasks (for overview).
    /// </summary>
    /// <param name="limit">Maximum number of tasks to return (default 100)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of tasks</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AgentTaskDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AgentTaskDto>>> GetAllTasks(
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        var tasks = await _taskService.GetAllTasksAsync(limit, ct);
        return Ok(tasks);
    }

    /// <summary>
    /// Approve a task for sending.
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <param name="ct">Cancellation token</param>
    [HttpPost("{taskId:int}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ApproveTask(int taskId, CancellationToken ct)
    {
        try
        {
            await _taskService.ApproveTaskAsync(taskId, ct);
            _logger.LogInformation("Task {Id} approved", taskId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
    }

    /// <summary>
    /// Cancel a pending task.
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <param name="ct">Cancellation token</param>
    [HttpPost("{taskId:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> CancelTask(int taskId, CancellationToken ct)
    {
        try
        {
            await _taskService.CancelTaskAsync(taskId, ct);
            _logger.LogInformation("Task {Id} cancelled", taskId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
    }

    /// <summary>
    /// Complete a task with a result.
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <param name="request">Completion request with result</param>
    /// <param name="ct">Cancellation token</param>
    [HttpPost("{taskId:int}/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> CompleteTask(
        int taskId,
        [FromBody] TaskCompleteRequest request,
        CancellationToken ct)
    {
        try
        {
            await _taskService.CompleteTaskAsync(taskId, request.Result, ct);
            _logger.LogInformation("Task {Id} completed", taskId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
    }

    /// <summary>
    /// Check if an agent is idle.
    /// </summary>
    /// <param name="agent">Agent name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Idle status</returns>
    [HttpGet("idle/{agent}")]
    [ProducesResponseType(typeof(AgentIdleResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AgentIdleResponse>> IsAgentIdle(string agent, CancellationToken ct)
    {
        var isIdle = await _taskService.IsAgentIdleAsync(agent, ct);
        return Ok(new AgentIdleResponse { Agent = agent, IsIdle = isIdle });
    }

    /// <summary>
    /// Get tasks ready to be sent (approved and target agent is idle).
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of tasks ready to send</returns>
    [HttpGet("ready-to-send")]
    [ProducesResponseType(typeof(IEnumerable<AgentTaskDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AgentTaskDto>>> GetReadyToSend(CancellationToken ct)
    {
        var tasks = await _taskService.GetReadyToSendAsync(ct);
        return Ok(tasks);
    }

    /// <summary>
    /// Get notified tasks for a specific agent (pull-based delivery).
    /// Returns tasks that have been notified to the agent but not yet accepted.
    /// </summary>
    /// <param name="agent">Agent name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of notified tasks</returns>
    [HttpGet("notified/{agent}")]
    [ProducesResponseType(typeof(IEnumerable<AgentTaskDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AgentTaskDto>>> GetNotifiedTasks(string agent, CancellationToken ct)
    {
        var tasks = await _taskService.GetNotifiedTasksAsync(agent, ct);
        return Ok(tasks);
    }

    /// <summary>
    /// Accept a notified task and receive the task prompt (pull-based delivery).
    /// Called by agent when ready to process a previously notified task.
    /// After accepting, the task transitions to 'sent' status via hub.
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task prompt content</returns>
    [HttpPost("{taskId:int}/accept")]
    [ProducesResponseType(typeof(TaskAcceptResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskAcceptResponse>> AcceptTask(int taskId, CancellationToken ct)
    {
        try
        {
            var prompt = await _taskService.AcceptTaskAsync(taskId, ct);
            _logger.LogInformation("Task {Id} accepted", taskId);
            return Ok(new TaskAcceptResponse { TaskId = taskId, Prompt = prompt });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
    }
}

/// <summary>
/// Request model for completing a task.
/// </summary>
public class TaskCompleteRequest
{
    /// <summary>
    /// Result/outcome description.
    /// </summary>
    public string Result { get; set; } = string.Empty;
}

/// <summary>
/// Response model for agent idle check.
/// </summary>
public class AgentIdleResponse
{
    /// <summary>
    /// Agent name.
    /// </summary>
    public string Agent { get; set; } = string.Empty;

    /// <summary>
    /// Whether the agent is idle.
    /// </summary>
    public bool IsIdle { get; set; }
}

/// <summary>
/// Response model for accepting a task.
/// </summary>
public class TaskAcceptResponse
{
    /// <summary>
    /// Task ID that was accepted.
    /// </summary>
    public int TaskId { get; set; }

    /// <summary>
    /// The task prompt content for the agent to process.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;
}
