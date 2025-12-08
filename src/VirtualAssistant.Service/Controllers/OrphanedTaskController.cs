using Microsoft.AspNetCore.Mvc;
using VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Controllers;

/// <summary>
/// REST API controller for managing orphaned tasks.
/// Provides endpoints for human decision-making after service restart.
/// </summary>
[ApiController]
[Route("api/orphaned-tasks")]
[Produces("application/json")]
public class OrphanedTaskController : ControllerBase
{
    private readonly IOrphanedTaskService _orphanedTaskService;
    private readonly IVirtualAssistantSpeaker _speaker;
    private readonly ILogger<OrphanedTaskController> _logger;

    public OrphanedTaskController(
        IOrphanedTaskService orphanedTaskService,
        IVirtualAssistantSpeaker speaker,
        ILogger<OrphanedTaskController> logger)
    {
        _orphanedTaskService = orphanedTaskService;
        _speaker = speaker;
        _logger = logger;
    }

    /// <summary>
    /// List all orphaned tasks.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of orphaned tasks</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<OrphanedTaskInfo>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrphanedTaskInfo>>> GetOrphanedTasks(CancellationToken ct)
    {
        var tasks = await _orphanedTaskService.FindOrphanedTasksAsync(ct);
        return Ok(tasks);
    }

    /// <summary>
    /// Mark an orphaned task as completed (human decision: "it's done").
    /// </summary>
    /// <param name="id">AgentResponse ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success confirmation</returns>
    [HttpPost("{id}/mark-completed")]
    [ProducesResponseType(typeof(OrphanedTaskActionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrphanedTaskActionResult>> MarkAsCompleted(int id, CancellationToken ct)
    {
        _logger.LogInformation("Human decision: mark orphaned task {Id} as completed", id);

        await _orphanedTaskService.MarkAsCompletedAsync(id, ct);

        await _speaker.SpeakAsync("Úkol označen jako dokončený.", agentName: null, ct);

        return Ok(new OrphanedTaskActionResult
        {
            Success = true,
            Action = "completed",
            AgentResponseId = id,
            Message = "Task marked as completed"
        });
    }

    /// <summary>
    /// Reset an orphaned task to pending (human decision: "try again").
    /// </summary>
    /// <param name="id">AgentResponse ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success confirmation</returns>
    [HttpPost("{id}/reset")]
    [ProducesResponseType(typeof(OrphanedTaskActionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrphanedTaskActionResult>> ResetTask(int id, CancellationToken ct)
    {
        _logger.LogInformation("Human decision: reset orphaned task {Id} to pending", id);

        await _orphanedTaskService.ResetTaskAsync(id, ct);

        await _speaker.SpeakAsync("Úkol resetován, zkusím znovu.", agentName: null, ct);

        return Ok(new OrphanedTaskActionResult
        {
            Success = true,
            Action = "reset",
            AgentResponseId = id,
            Message = "Task reset to pending"
        });
    }

    /// <summary>
    /// Ignore an orphaned task (human decision: "leave it").
    /// </summary>
    /// <param name="id">AgentResponse ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success confirmation</returns>
    [HttpPost("{id}/ignore")]
    [ProducesResponseType(typeof(OrphanedTaskActionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrphanedTaskActionResult>> Ignore(int id, CancellationToken ct)
    {
        _logger.LogInformation("Human decision: ignore orphaned task {Id}", id);

        await _orphanedTaskService.IgnoreAsync(id, ct);

        await _speaker.SpeakAsync("Úkol ignorován.", agentName: null, ct);

        return Ok(new OrphanedTaskActionResult
        {
            Success = true,
            Action = "ignored",
            AgentResponseId = id,
            Message = "Task ignored"
        });
    }
}

/// <summary>
/// Result of an orphaned task action.
/// </summary>
public class OrphanedTaskActionResult
{
    /// <summary>
    /// Whether the action succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Action taken: "completed", "reset", "ignored".
    /// </summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// AgentResponse ID that was acted upon.
    /// </summary>
    public int AgentResponseId { get; init; }

    /// <summary>
    /// Human-readable message.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
