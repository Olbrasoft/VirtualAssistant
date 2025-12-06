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
