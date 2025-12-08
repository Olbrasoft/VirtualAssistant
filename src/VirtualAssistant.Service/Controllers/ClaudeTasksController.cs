using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VirtualAssistant.Data.Dtos;
using VirtualAssistant.Data.EntityFrameworkCore;

namespace Olbrasoft.VirtualAssistant.Service.Controllers;

/// <summary>
/// REST API controller for Claude Code task operations.
/// Provides simplified endpoints for Claude Code plugin to fetch pending tasks and report completion.
/// </summary>
[ApiController]
[Route("api/claude/tasks")]
[Produces("application/json")]
public class ClaudeTasksController : ControllerBase
{
    private readonly VirtualAssistantDbContext _dbContext;
    private readonly ILogger<ClaudeTasksController> _logger;

    /// <summary>
    /// Initializes a new instance of the ClaudeTasksController.
    /// </summary>
    /// <param name="dbContext">The database context</param>
    /// <param name="logger">The logger</param>
    public ClaudeTasksController(
        VirtualAssistantDbContext dbContext,
        ILogger<ClaudeTasksController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Get the oldest pending task for the Claude agent.
    /// Returns the oldest task with status pending, approved, or notified.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task details or 204 No Content if no pending tasks</returns>
    /// <response code="200">Task found and returned</response>
    /// <response code="204">No pending tasks available</response>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(ClaudePendingTaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetPendingTask(CancellationToken ct)
    {
        // Find the oldest task for Claude agent with status pending, approved, or notified
        var validStatuses = new[] { "pending", "approved", "notified" };

        var task = await _dbContext.AgentTasks
            .Include(t => t.TargetAgent)
            .Where(t => t.TargetAgent != null && t.TargetAgent.Name == "claude")
            .Where(t => validStatuses.Contains(t.Status))
            .OrderBy(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (task == null)
        {
            _logger.LogDebug("No pending tasks for Claude agent");
            return NoContent();
        }

        // Update task status to "sent" and set SentAt timestamp
        task.Status = "sent";
        task.SentAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Task {TaskId} fetched by Claude, issue #{IssueNumber}, status changed to 'sent'",
            task.Id, task.GithubIssueNumber);

        return Ok(new ClaudePendingTaskResponse
        {
            Id = task.Id,
            GithubIssueUrl = task.GithubIssueUrl,
            GithubIssueNumber = task.GithubIssueNumber,
            Summary = task.Summary,
            CreatedAt = task.CreatedAt
        });
    }

    /// <summary>
    /// Mark a task as completed with the result summary.
    /// </summary>
    /// <param name="id">Task ID</param>
    /// <param name="request">Completion details including session ID and result</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Completion confirmation</returns>
    /// <response code="200">Task marked as completed</response>
    /// <response code="400">Invalid request body</response>
    /// <response code="404">Task not found</response>
    [HttpPost("{id:int}/complete")]
    [ProducesResponseType(typeof(ClaudeCompleteTaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteTask(
        int id,
        [FromBody] ClaudeCompleteTaskRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Result))
        {
            return BadRequest(new ErrorResponse { Error = "Result is required" });
        }

        var task = await _dbContext.AgentTasks.FindAsync(new object[] { id }, ct);

        if (task == null)
        {
            _logger.LogWarning("Task {TaskId} not found for completion", id);
            return NotFound(new ErrorResponse { Error = $"Task {id} not found" });
        }

        // Update task
        task.Status = "completed";
        task.CompletedAt = DateTime.UtcNow;
        task.Result = request.Result;

        if (!string.IsNullOrWhiteSpace(request.SessionId))
        {
            task.ClaudeSessionId = request.SessionId;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Task {TaskId} marked as completed, issue #{IssueNumber}",
            task.Id, task.GithubIssueNumber);

        return Ok(new ClaudeCompleteTaskResponse
        {
            Id = task.Id,
            Status = task.Status,
            CompletedAt = task.CompletedAt.Value
        });
    }
}
