using System.Diagnostics;
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
    private readonly IAgentTaskService _taskService;
    private readonly ILogger<AgentHubController> _logger;

    /// <summary>
    /// Initializes a new instance of the AgentHubController.
    /// </summary>
    /// <param name="hubService">The agent hub service</param>
    /// <param name="taskService">The agent task service</param>
    /// <param name="logger">The logger</param>
    public AgentHubController(
        IAgentHubService hubService,
        IAgentTaskService taskService,
        ILogger<AgentHubController> logger)
    {
        _hubService = hubService;
        _taskService = taskService;
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
    /// Copy content to system clipboard using xclip.
    /// </summary>
    /// <param name="request">Clipboard request with content</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="200">Content copied to clipboard</response>
    /// <response code="400">Invalid request or clipboard operation failed</response>
    [HttpPost("clipboard")]
    [ProducesResponseType(typeof(ClipboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClipboardResponse>> ToClipboard(
        [FromBody] ClipboardRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.Content))
        {
            return BadRequest(new ErrorResponse { Error = "Content cannot be empty" });
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "xclip",
                    Arguments = "-selection clipboard",
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.StandardInput.WriteAsync(request.Content);
            process.StandardInput.Close();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            await process.WaitForExitAsync(cts.Token);

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync(ct);
                _logger.LogWarning("xclip failed with exit code {Code}: {Error}", process.ExitCode, error);
                return BadRequest(new ErrorResponse { Error = $"Clipboard operation failed: {error}" });
            }

            _logger.LogInformation("Content copied to clipboard ({Length} chars)", request.Content.Length);

            return Ok(new ClipboardResponse
            {
                Success = true,
                Message = "Content copied to clipboard"
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to copy to clipboard");
            return BadRequest(new ErrorResponse { Error = $"Clipboard operation failed: {ex.Message}" });
        }
    }

    /// <summary>
    /// Dispatch a task to an agent (typically Claude).
    /// Checks if agent is available and sends first pending task.
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

        var result = await _taskService.DispatchTaskAsync(
            targetAgent,
            request?.GithubIssueNumber,
            request?.GithubIssueUrl,
            ct);

        return Ok(new DispatchTaskResponse
        {
            Success = result.Success,
            Reason = result.Reason,
            Message = result.Message,
            TaskId = result.TaskId,
            GithubIssueNumber = result.GithubIssueNumber,
            GithubIssueUrl = result.GithubIssueUrl,
            Summary = result.Summary
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
