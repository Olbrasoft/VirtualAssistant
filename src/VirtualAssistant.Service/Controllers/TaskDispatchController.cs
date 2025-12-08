using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VirtualAssistant.Core.Services;
using VirtualAssistant.Data.Dtos;
using VirtualAssistant.Data.Entities;
using VirtualAssistant.Data.EntityFrameworkCore;
using VirtualAssistant.Data.Enums;

namespace Olbrasoft.VirtualAssistant.Service.Controllers;

/// <summary>
/// REST API controller for task dispatch operations.
/// Provides endpoints for fetching, dispatching, and completing agent tasks.
/// </summary>
[ApiController]
[Route("api/hub")]
[Produces("application/json")]
public class TaskDispatchController : ControllerBase
{
    private readonly IAgentTaskService _taskService;
    private readonly IClaudeDispatchService _claudeService;
    private readonly VirtualAssistantDbContext _dbContext;
    private readonly ILogger<TaskDispatchController> _logger;

    /// <summary>
    /// Initializes a new instance of the TaskDispatchController.
    /// </summary>
    /// <param name="taskService">The agent task service</param>
    /// <param name="claudeService">The Claude dispatch service</param>
    /// <param name="dbContext">The database context</param>
    /// <param name="logger">The logger</param>
    public TaskDispatchController(
        IAgentTaskService taskService,
        IClaudeDispatchService claudeService,
        VirtualAssistantDbContext dbContext,
        ILogger<TaskDispatchController> logger)
    {
        _taskService = taskService;
        _claudeService = claudeService;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Fetch the oldest pending task for an agent and mark it as sent.
    /// Used for pull-based task delivery (e.g., OpenCode fetching tasks from Claude).
    /// </summary>
    /// <param name="agent">Agent identifier (e.g., "opencode")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task content or null if no pending tasks</returns>
    /// <response code="200">Task fetched successfully or no tasks available</response>
    /// <response code="400">Invalid agent identifier</response>
    [HttpGet("fetch-task/{agent}")]
    [ProducesResponseType(typeof(FetchTaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FetchTaskResponse>> FetchTask(
        string agent,
        CancellationToken ct)
    {
        try
        {
            // Get notified tasks for this agent (oldest first)
            var notifiedTasks = await _taskService.GetNotifiedTasksAsync(agent, ct);

            if (notifiedTasks.Count == 0)
            {
                _logger.LogDebug("No pending tasks for agent {Agent}", agent);
                return Ok(new FetchTaskResponse
                {
                    Success = true,
                    Task = null,
                    Message = "No pending tasks"
                });
            }

            // Get the oldest task
            var oldestTask = notifiedTasks[0];

            // Accept the task (marks as sent and returns the prompt)
            var content = await _taskService.AcceptTaskAsync(oldestTask.Id, ct);

            _logger.LogInformation(
                "Task {TaskId} fetched by {Agent}, issue #{Issue}",
                oldestTask.Id, agent, oldestTask.GithubIssueNumber);

            return Ok(new FetchTaskResponse
            {
                Success = true,
                Task = new FetchedTaskInfo
                {
                    Id = oldestTask.Id,
                    FromAgent = oldestTask.CreatedByAgent ?? "unknown",
                    Content = content,
                    CreatedAt = oldestTask.CreatedAt,
                    GithubIssue = oldestTask.GithubIssueNumber.HasValue
                        ? $"#{oldestTask.GithubIssueNumber}"
                        : null
                }
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid agent identifier: {Agent}", agent);
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Task not found while fetching for {Agent}", agent);
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot accept task for {Agent}", agent);
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
    }

    /// <summary>
    /// Dispatch a task to an agent (typically Claude).
    /// Checks if agent is available, finds pending task, and executes Claude Code in headless mode.
    /// </summary>
    /// <param name="request">Optional dispatch parameters</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Dispatch result</returns>
    /// <response code="200">Dispatch result (success or failure reason)</response>
    [HttpPost("dispatch-task")]
    [ProducesResponseType(typeof(DispatchTaskResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DispatchTaskResponse>> DispatchTask(
        [FromBody] DispatchTaskRequest? request,
        CancellationToken ct)
    {
        // Default to Claude if no target specified
        var targetAgent = request?.TargetAgent ?? "claude";

        _logger.LogInformation(
            "Dispatch request for {Agent}, issue: {IssueNumber}/{IssueUrl}",
            targetAgent,
            request?.GithubIssueNumber?.ToString() ?? "(none)",
            request?.GithubIssueUrl ?? "(none)");

        // Step 1: Find and mark task as sent (existing logic)
        var result = await _taskService.DispatchTaskAsync(
            targetAgent,
            request?.GithubIssueNumber,
            request?.GithubIssueUrl,
            ct);

        // If dispatch failed (no task, agent busy, etc.), return immediately
        if (!result.Success)
        {
            return Ok(new DispatchTaskResponse
            {
                Success = false,
                Reason = result.Reason,
                Message = result.Message,
                TaskId = result.TaskId,
                GithubIssueNumber = result.GithubIssueNumber,
                GithubIssueUrl = result.GithubIssueUrl,
                Summary = result.Summary
            });
        }

        // Step 2: Only execute Claude if target is claude
        string? sessionId = null;
        int? agentResponseId = null;

        if (targetAgent.Equals("claude", StringComparison.OrdinalIgnoreCase))
        {
            // Create AgentResponse record to track agent status (linked to task)
            var agentResponse = new AgentResponse
            {
                AgentName = "claude",
                Status = AgentResponseStatus.InProgress,
                StartedAt = DateTime.UtcNow,
                AgentTaskId = result.TaskId
            };

            _dbContext.AgentResponses.Add(agentResponse);
            await _dbContext.SaveChangesAsync(ct);
            agentResponseId = agentResponse.Id;

            _logger.LogInformation(
                "Created AgentResponse {ResponseId} for task {TaskId}",
                agentResponse.Id, result.TaskId);

            // Build the prompt for Claude
            var prompt = $"""
                Nový úkol k implementaci:
                {result.Summary}

                Issue: {result.GithubIssueUrl}

                Přečti si issue pro detaily, implementuj, otestuj, nasaď.
                Po dokončení zavolej /api/hub/complete-task s výsledkem.
                """;

            _logger.LogInformation(
                "Executing Claude headless mode for task {TaskId}, issue #{IssueNumber}",
                result.TaskId, result.GithubIssueNumber);

            // Capture values for closure BEFORE entering Task.Run
            // HttpContext becomes null after the HTTP request completes
            var taskId = result.TaskId;
            var responseId = agentResponse.Id;
            var serviceProvider = HttpContext.RequestServices;

            // Execute Claude in headless mode (fire and forget - runs in background)
            // We use Task.Run to not block the response
            _ = Task.Run(async () =>
            {
                try
                {
                    var claudeResult = await _claudeService.ExecuteAsync(prompt);

                    // Update AgentResponse and AgentTask with results
                    using var scope = serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<VirtualAssistantDbContext>();

                    var response = await db.AgentResponses.FindAsync(responseId);
                    if (response != null)
                    {
                        response.Status = claudeResult.Success
                            ? AgentResponseStatus.Completed
                            : AgentResponseStatus.Completed; // Mark completed even on error (agent is no longer busy)
                        response.CompletedAt = DateTime.UtcNow;
                        await db.SaveChangesAsync();
                    }

                    if (claudeResult.Success && !string.IsNullOrEmpty(claudeResult.SessionId))
                    {
                        // Update task with Claude session ID
                        var task = await db.AgentTasks.FindAsync(taskId);
                        if (task != null)
                        {
                            task.ClaudeSessionId = claudeResult.SessionId;
                            await db.SaveChangesAsync();
                        }

                        _logger.LogInformation(
                            "Claude completed task {TaskId}, session: {SessionId}",
                            taskId, claudeResult.SessionId);
                    }
                    else if (!claudeResult.Success)
                    {
                        _logger.LogError(
                            "Claude execution failed for task {TaskId}: {Error}",
                            taskId, claudeResult.Error);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing Claude for task {TaskId}", taskId);

                    // Mark AgentResponse as completed even on exception so agent doesn't stay "busy" forever
                    try
                    {
                        using var scope = serviceProvider.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<VirtualAssistantDbContext>();
                        var response = await db.AgentResponses.FindAsync(responseId);
                        if (response != null)
                        {
                            response.Status = AgentResponseStatus.Completed;
                            response.CompletedAt = DateTime.UtcNow;
                            await db.SaveChangesAsync();
                        }
                    }
                    catch (Exception innerEx)
                    {
                        _logger.LogError(innerEx, "Failed to mark AgentResponse {ResponseId} as completed after error", responseId);
                    }
                }
            }, CancellationToken.None);

            // Return immediately - Claude runs in background
        }

        return Ok(new DispatchTaskResponse
        {
            Success = true,
            Reason = "dispatched",
            Message = result.Message,
            TaskId = result.TaskId,
            GithubIssueNumber = result.GithubIssueNumber,
            GithubIssueUrl = result.GithubIssueUrl,
            Summary = result.Summary,
            ClaudeSessionId = sessionId
        });
    }

    /// <summary>
    /// Complete a task with result (called by Claude).
    /// </summary>
    /// <param name="request">Completion request with task ID and result</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Completion confirmation</returns>
    /// <response code="200">Task completed successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="404">Task not found</response>
    [HttpPost("complete-task")]
    [ProducesResponseType(typeof(CompleteTaskApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompleteTaskApiResponse>> CompleteAgentTask(
        [FromBody] CompleteTaskApiRequest request,
        CancellationToken ct)
    {
        // Find task by ID or issue number
        int? taskId = request.TaskId;

        if (taskId == null && request.GithubIssueNumber.HasValue)
        {
            // Find task by issue number
            var tasks = await _taskService.GetAllTasksAsync(1000, ct);
            var task = tasks.FirstOrDefault(t =>
                t.GithubIssueNumber == request.GithubIssueNumber &&
                t.Status == "sent");

            if (task == null)
            {
                return NotFound(new ErrorResponse
                {
                    Error = $"No sent task found for issue #{request.GithubIssueNumber}"
                });
            }

            taskId = task.Id;
        }

        if (taskId == null)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "Either task_id or github_issue_number is required"
            });
        }

        // Validate status
        var validStatuses = new[] { "completed", "failed", "blocked" };
        var status = request.Status?.ToLowerInvariant() ?? "completed";
        if (!validStatuses.Contains(status))
        {
            return BadRequest(new ErrorResponse
            {
                Error = $"Invalid status '{request.Status}'. Valid values: completed, failed, blocked"
            });
        }

        try
        {
            // For completed status, use existing CompleteTaskAsync
            // For failed/blocked, we need to update directly
            if (status == "completed")
            {
                await _taskService.CompleteTaskAsync(taskId.Value, request.Result ?? "", ct);
            }
            else
            {
                // Get task and update status manually for failed/blocked
                var task = await _taskService.GetTaskAsync(taskId.Value, ct);
                if (task == null)
                {
                    return NotFound(new ErrorResponse { Error = $"Task {taskId} not found" });
                }

                // Use the existing complete method but we'll need to handle this differently
                // For now, treat failed/blocked as completed with status in result
                await _taskService.CompleteTaskAsync(taskId.Value,
                    $"[{status.ToUpperInvariant()}] {request.Result}", ct);
            }

            _logger.LogInformation(
                "Task {TaskId} marked as {Status} by agent",
                taskId, status);

            // Auto-dispatch: If task completed successfully and auto_dispatch is enabled (default: true)
            // try to dispatch the next pending task to the same agent
            DispatchTaskResult? nextTaskResult = null;
            var autoDispatch = request.AutoDispatch ?? true; // Default to true

            if (autoDispatch && status == "completed")
            {
                // Find the original task to get target agent
                var completedTask = await _taskService.GetTaskAsync(taskId.Value, ct);
                var targetAgent = completedTask?.TargetAgent ?? "claude";

                _logger.LogInformation("Auto-dispatch enabled, checking for next task for {Agent}", targetAgent);

                // Try to dispatch next task
                nextTaskResult = await _taskService.DispatchTaskAsync(targetAgent, null, null, ct);

                if (nextTaskResult.Success)
                {
                    _logger.LogInformation(
                        "Auto-dispatched task {TaskId} (issue #{IssueNumber}) to {Agent}",
                        nextTaskResult.TaskId, nextTaskResult.GithubIssueNumber, targetAgent);
                }
                else
                {
                    _logger.LogInformation(
                        "Auto-dispatch: {Reason}",
                        nextTaskResult.Reason ?? "No pending tasks");
                }
            }

            return Ok(new CompleteTaskApiResponse
            {
                Success = true,
                TaskId = taskId.Value,
                Message = $"Task marked as {status}",
                NextTask = nextTaskResult?.Success == true ? new NextTaskInfo
                {
                    TaskId = nextTaskResult.TaskId!.Value,
                    GithubIssueNumber = nextTaskResult.GithubIssueNumber,
                    Summary = nextTaskResult.Summary
                } : null
            });
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
    /// Get task status by ID or issue number.
    /// </summary>
    /// <param name="taskId">Task ID (optional if using issue query param)</param>
    /// <param name="issue">GitHub issue number (alternative to taskId)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task status details</returns>
    /// <response code="200">Task status</response>
    /// <response code="404">Task not found</response>
    [HttpGet("task-status/{taskId:int?}")]
    [ProducesResponseType(typeof(TaskStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskStatusResponse>> GetTaskStatus(
        int? taskId,
        [FromQuery] int? issue,
        CancellationToken ct)
    {
        AgentTaskDto? task = null;

        if (taskId.HasValue)
        {
            task = await _taskService.GetTaskAsync(taskId.Value, ct);
        }
        else if (issue.HasValue)
        {
            // Find by issue number
            var tasks = await _taskService.GetAllTasksAsync(1000, ct);
            task = tasks.FirstOrDefault(t => t.GithubIssueNumber == issue);
        }
        else
        {
            return BadRequest(new ErrorResponse
            {
                Error = "Either taskId path parameter or issue query parameter is required"
            });
        }

        if (task == null)
        {
            return NotFound(new ErrorResponse
            {
                Error = taskId.HasValue
                    ? $"Task {taskId} not found"
                    : $"No task found for issue #{issue}"
            });
        }

        return Ok(new TaskStatusResponse
        {
            TaskId = task.Id,
            GithubIssueNumber = task.GithubIssueNumber,
            GithubIssueUrl = task.GithubIssueUrl,
            Status = task.Status,
            Summary = task.Summary,
            Result = task.Result,
            CreatedByAgent = task.CreatedByAgent,
            TargetAgent = task.TargetAgent,
            CreatedAt = task.CreatedAt,
            SentAt = task.SentAt,
            CompletedAt = task.CompletedAt
        });
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
    [HttpPost("/api/tasks/create-and-dispatch")] // Alias for OpenCode plugin
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

            // If target is Claude, execute in background (same as dispatch-task endpoint)
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
                    Nový úkol k implementaci:
                    {task.Summary}

                    Issue: {task.GithubIssueUrl}

                    Přečti si issue pro detaily, implementuj, otestuj, nasaď.
                    Po dokončení zavolej /api/hub/complete-task s výsledkem.
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
                        var claudeResult = await _claudeService.ExecuteAsync(prompt);

                        using var scope = serviceProvider.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<VirtualAssistantDbContext>();

                        var response = await db.AgentResponses.FindAsync(responseId);
                        if (response != null)
                        {
                            response.Status = AgentResponseStatus.Completed;
                            response.CompletedAt = DateTime.UtcNow;
                            await db.SaveChangesAsync();
                        }

                        if (claudeResult.Success && !string.IsNullOrEmpty(claudeResult.SessionId))
                        {
                            var taskEntity = await db.AgentTasks.FindAsync(taskId);
                            if (taskEntity != null)
                            {
                                taskEntity.ClaudeSessionId = claudeResult.SessionId;
                                await db.SaveChangesAsync();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing Claude for task {TaskId}", taskId);

                        try
                        {
                            using var scope = serviceProvider.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<VirtualAssistantDbContext>();
                            var response = await db.AgentResponses.FindAsync(responseId);
                            if (response != null)
                            {
                                response.Status = AgentResponseStatus.Completed;
                                response.CompletedAt = DateTime.UtcNow;
                                await db.SaveChangesAsync();
                            }
                        }
                        catch { }
                    }
                }, CancellationToken.None);
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
                : $"Task created, Claude is busy - queued for later",
            "reopened" => dispatchStatus == "sent_to_claude"
                ? "Task reopened and dispatched to Claude"
                : $"Task reopened, Claude is busy - queued for later",
            "dispatched" => dispatchStatus == "sent_to_claude"
                ? "Task dispatched to Claude"
                : $"Task queued, Claude is busy",
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
    /// Queue a task for another agent.
    /// Simplified endpoint for MCP tool usage - no header required.
    /// </summary>
    /// <param name="request">Task details</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task ID and confirmation</returns>
    /// <response code="200">Task queued successfully</response>
    /// <response code="400">Invalid request data</response>
    [HttpPost("send-task")]
    [ProducesResponseType(typeof(SendTaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SendTaskResponse>> SendTask(
        [FromBody] SendTaskRequest request,
        CancellationToken ct)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.SourceAgent))
        {
            return BadRequest(new ErrorResponse { Error = "source_agent is required" });
        }

        if (string.IsNullOrWhiteSpace(request.TargetAgent))
        {
            return BadRequest(new ErrorResponse { Error = "target_agent is required" });
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new ErrorResponse { Error = "content is required" });
        }

        try
        {
            // Build GitHub issue URL from short format if provided
            var githubIssueUrl = string.Empty;
            if (!string.IsNullOrWhiteSpace(request.GithubIssue))
            {
                // Convert "#123" or "123" to full URL
                var issueNumber = request.GithubIssue.TrimStart('#');
                if (int.TryParse(issueNumber, out _))
                {
                    githubIssueUrl = $"https://github.com/Olbrasoft/VirtualAssistant/issues/{issueNumber}";
                }
            }

            // Create the task using existing service
            var createRequest = new CreateTaskRequest
            {
                GithubIssueUrl = githubIssueUrl,
                Summary = request.Content,
                TargetAgent = request.TargetAgent,
                RequiresApproval = request.Priority?.ToLowerInvariant() != "high" // High priority = no approval needed
            };

            var task = await _taskService.CreateTaskAsync(request.SourceAgent, createRequest, ct);

            _logger.LogInformation(
                "Task {TaskId} queued from {Source} to {Target}, issue: {Issue}",
                task.Id, request.SourceAgent, request.TargetAgent, request.GithubIssue ?? "(none)");

            return Ok(new SendTaskResponse
            {
                Success = true,
                TaskId = task.Id,
                Message = $"Task queued for {request.TargetAgent}"
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create task from {Source} to {Target}",
                request.SourceAgent, request.TargetAgent);
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid task request");
            return BadRequest(new ErrorResponse { Error = ex.Message });
        }
    }
}
