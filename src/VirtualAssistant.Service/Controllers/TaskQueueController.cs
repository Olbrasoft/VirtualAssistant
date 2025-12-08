using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VirtualAssistant.Core.Services;
using VirtualAssistant.Data.Dtos;
using VirtualAssistant.Data.Entities;
using VirtualAssistant.Data.EntityFrameworkCore;
using VirtualAssistant.Data.Enums;

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
    private readonly IClaudeDispatchService _claudeDispatch;
    private readonly VirtualAssistantDbContext _dbContext;
    private readonly ILogger<TaskQueueController> _logger;

    public TaskQueueController(
        IAgentTaskService taskService,
        IClaudeDispatchService claudeDispatch,
        VirtualAssistantDbContext dbContext,
        ILogger<TaskQueueController> logger)
    {
        _taskService = taskService;
        _claudeDispatch = claudeDispatch;
        _dbContext = dbContext;
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

    /// <summary>
    /// Create or reopen a task and dispatch to Claude in one atomic operation.
    /// Upsert logic: creates if not exists, reopens if completed/sent/cancelled, dispatches if pending.
    /// </summary>
    /// <param name="request">Task details with github_issue_number</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result with action taken and dispatch status</returns>
    /// <response code="200">Task created/reopened and dispatched (or queued if agent busy)</response>
    /// <response code="400">Invalid request (missing required fields)</response>
    [HttpPost("create-and-dispatch")]
    [ProducesResponseType(typeof(CreateAndDispatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(CreateAndDispatchResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateAndDispatchResponse>> CreateAndDispatch(
        [FromBody] CreateAndDispatchRequest request,
        CancellationToken ct)
    {
        // Validate required field
        if (!request.GithubIssueNumber.HasValue)
        {
            return BadRequest(new CreateAndDispatchResponse
            {
                Success = false,
                Error = "github_issue_number is required"
            });
        }

        var targetAgent = request.TargetAgent ?? "claude";
        var issueNumber = request.GithubIssueNumber.Value;

        _logger.LogInformation(
            "Create-and-dispatch request for issue #{IssueNumber}, target: {Agent}",
            issueNumber, targetAgent);

        // Get the target agent entity
        var agent = await _dbContext.Agents
            .FirstOrDefaultAsync(a => a.Name == targetAgent && a.IsActive, ct);

        if (agent == null)
        {
            return BadRequest(new CreateAndDispatchResponse
            {
                Success = false,
                Error = $"Agent '{targetAgent}' not found"
            });
        }

        // Find existing task by github_issue_number
        var task = await _dbContext.AgentTasks
            .Include(t => t.CreatedByAgent)
            .Include(t => t.TargetAgent)
            .FirstOrDefaultAsync(t => t.GithubIssueNumber == issueNumber, ct);

        string action;
        string? previousStatus = null;

        if (task == null)
        {
            // CREATE new task
            task = new AgentTask
            {
                GithubIssueNumber = issueNumber,
                GithubIssueUrl = $"https://github.com/Olbrasoft/VirtualAssistant/issues/{issueNumber}",
                Summary = request.Summary ?? $"Task for issue #{issueNumber}",
                TargetAgentId = agent.Id,
                Status = "pending",
                RequiresApproval = false, // Auto-dispatch doesn't require approval
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.AgentTasks.Add(task);
            action = "created";

            _logger.LogInformation("Creating new task for issue #{IssueNumber}", issueNumber);
        }
        else if (task.Status == "pending" || task.Status == "approved")
        {
            // Already pending - just dispatch
            action = "dispatched";
            _logger.LogInformation("Task {TaskId} already pending, will dispatch", task.Id);
        }
        else
        {
            // REOPEN - clear timestamps, set pending
            previousStatus = task.Status;
            task.Status = "pending";
            task.CompletedAt = null;
            task.SentAt = null;
            task.NotifiedAt = null;
            task.ApprovedAt = null;
            task.Result = null;
            task.ClaudeSessionId = null;
            task.TargetAgentId = agent.Id; // Update target agent
            if (!string.IsNullOrEmpty(request.Summary))
            {
                task.Summary = request.Summary;
            }
            action = "reopened";

            _logger.LogInformation(
                "Reopening task {TaskId} from status '{PreviousStatus}'",
                task.Id, previousStatus);
        }

        await _dbContext.SaveChangesAsync(ct);

        // Now dispatch to Claude
        var dispatchResult = await _taskService.DispatchTaskAsync(targetAgent, issueNumber, null, ct);

        string dispatchStatus;
        string? reason = null;

        if (dispatchResult.Success)
        {
            dispatchStatus = "sent_to_claude";

            // If target is Claude, execute in background
            if (targetAgent.Equals("claude", StringComparison.OrdinalIgnoreCase))
            {
                // Create AgentResponse record to track agent status
                var agentResponse = new AgentResponse
                {
                    AgentName = "claude",
                    Status = AgentResponseStatus.InProgress,
                    StartedAt = DateTime.UtcNow,
                    AgentTaskId = task.Id
                };

                _dbContext.AgentResponses.Add(agentResponse);
                await _dbContext.SaveChangesAsync(ct);

                // Build the prompt for Claude
                var prompt = $"""
                    Implementuj GitHub issue #{issueNumber}:
                    {task.Summary}

                    Issue URL: {task.GithubIssueUrl}

                    Repozitář: ~/Olbrasoft/VirtualAssistant
                    Přečti si issue pro detaily, implementuj, otestuj, nasaď.
                    """;

                // Capture values for closure
                var taskId = task.Id;
                var responseId = agentResponse.Id;
                var serviceProvider = HttpContext.RequestServices;

                // Execute Claude in background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var claudeResult = await _claudeDispatch.ExecuteAsync(prompt, ct: CancellationToken.None);

                        await UpdateTaskCompletionAsync(
                            serviceProvider, responseId, taskId,
                            claudeResult.SessionId,
                            claudeResult.Success ? "completed" : "failed",
                            claudeResult.Success ? (claudeResult.Result ?? "Completed") : $"Error: {claudeResult.Error}");

                        if (claudeResult.Success)
                        {
                            await _claudeDispatch.NotifySuccessAsync($"Úkol číslo {issueNumber} byl dokončen.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing Claude for task {TaskId}", taskId);

                        await UpdateTaskCompletionAsync(
                            serviceProvider, responseId, taskId,
                            sessionId: null,
                            status: "failed",
                            taskResult: $"Exception: {ex.Message}");
                    }
                });
            }
        }
        else
        {
            dispatchStatus = "queued";
            reason = dispatchResult.Reason;

            _logger.LogInformation(
                "Task {TaskId} queued, dispatch failed: {Reason}",
                task.Id, reason);
        }

        var message = action switch
        {
            "created" => dispatchStatus == "sent_to_claude"
                ? "Task created and dispatched to Claude"
                : "Task created, Claude is busy - queued for later",
            "reopened" => dispatchStatus == "sent_to_claude"
                ? "Task reopened and dispatched to Claude"
                : "Task reopened, Claude is busy - queued for later",
            "dispatched" => dispatchStatus == "sent_to_claude"
                ? "Task dispatched to Claude"
                : "Task queued, Claude is busy",
            _ => "Task processed"
        };

        return Ok(new CreateAndDispatchResponse
        {
            Success = true,
            Action = action,
            TaskId = task.Id,
            GithubIssueNumber = issueNumber,
            DispatchStatus = dispatchStatus,
            PreviousStatus = previousStatus,
            Reason = reason,
            Message = message
        });
    }

    /// <summary>
    /// Dispatch the first pending task to Claude via headless mode.
    /// Checks if Claude is idle, finds pending task, and executes claude -p command.
    /// </summary>
    /// <param name="request">Optional: specific issue to dispatch</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Dispatch result</returns>
    [HttpPost("dispatch")]
    [ProducesResponseType(typeof(DispatchTaskResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DispatchTaskResult), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DispatchTaskResult>> DispatchTask(
        [FromBody] DispatchTaskRequest? request = null,
        CancellationToken ct = default)
    {
        const string targetAgent = "claude";

        // 1. Find pending task and check if Claude is idle
        var result = await _taskService.DispatchTaskAsync(
            targetAgent,
            request?.GithubIssueNumber,
            request?.GithubIssueUrl,
            ct);

        if (!result.Success)
        {
            _logger.LogInformation("Dispatch rejected: {Reason}", result.Reason);
            return result.Reason == "agent_busy"
                ? Conflict(result)
                : Ok(result);
        }

        // 2. Create AgentResponse record to track agent status (linked to task)
        var agentResponse = new AgentResponse
        {
            AgentName = targetAgent,
            Status = AgentResponseStatus.InProgress,
            StartedAt = DateTime.UtcNow,
            AgentTaskId = result.TaskId
        };

        _dbContext.AgentResponses.Add(agentResponse);
        await _dbContext.SaveChangesAsync(ct);

        var responseId = agentResponse.Id;
        var taskId = result.TaskId!.Value;
        var issueNumber = result.GithubIssueNumber;

        _logger.LogInformation(
            "Task {TaskId} dispatched to Claude, AgentResponse {ResponseId} created",
            taskId, responseId);

        // 3. Build prompt for Claude
        var prompt = $"""
            Implementuj GitHub issue #{issueNumber}:
            {result.Summary}

            Issue URL: {result.GithubIssueUrl}

            Repozitář: ~/Olbrasoft/VirtualAssistant
            Přečti si issue pro detaily, implementuj, otestuj, nasaď.
            """;

        // 4. Execute Claude in background (fire-and-forget)
        var serviceProvider = HttpContext.RequestServices;
        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("Starting Claude execution for task {TaskId}", taskId);

                var claudeResult = await _claudeDispatch.ExecuteAsync(prompt, ct: CancellationToken.None);

                // Update database with results
                await UpdateTaskCompletionAsync(
                    serviceProvider, responseId, taskId,
                    claudeResult.SessionId,
                    claudeResult.Success ? "completed" : "failed",
                    claudeResult.Success ? (claudeResult.Result ?? "Completed") : $"Error: {claudeResult.Error}");

                _logger.LogInformation(
                    "Claude execution completed for task {TaskId}, success: {Success}, session: {Session}",
                    taskId, claudeResult.Success, claudeResult.SessionId);

                // Send success notification
                if (claudeResult.Success)
                {
                    await _claudeDispatch.NotifySuccessAsync($"Úkol číslo {issueNumber} byl dokončen.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Claude execution failed for task {TaskId}", taskId);

                // Mark as failed on exception
                await UpdateTaskCompletionAsync(
                    serviceProvider, responseId, taskId,
                    sessionId: null,
                    status: "failed",
                    taskResult: $"Exception: {ex.Message}");
            }
        });

        return Ok(result);
    }

    /// <summary>
    /// Updates task and response records after Claude execution.
    /// </summary>
    private static async Task UpdateTaskCompletionAsync(
        IServiceProvider serviceProvider,
        int responseId,
        int taskId,
        string? sessionId,
        string status,
        string taskResult)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VirtualAssistantDbContext>();

        var response = await db.AgentResponses.FindAsync(responseId);
        if (response != null)
        {
            response.Status = AgentResponseStatus.Completed;
            response.CompletedAt = DateTime.UtcNow;
        }

        var task = await db.AgentTasks.FindAsync(taskId);
        if (task != null)
        {
            task.ClaudeSessionId = sessionId;
            task.Status = status;
            task.CompletedAt = DateTime.UtcNow;
            task.Result = taskResult;
        }

        await db.SaveChangesAsync();
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
