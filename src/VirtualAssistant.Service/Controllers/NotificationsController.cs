using Microsoft.AspNetCore.Mvc;
using VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Controllers;

/// <summary>
/// REST API controller for notifications from agents.
/// </summary>
[ApiController]
[Route("api/notifications")]
[Produces("application/json")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationService notificationService,
        ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new notification from an agent.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> CreateNotification([FromBody] CreateNotificationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new ErrorResponse { Error = "Text is required" });
        }

        if (string.IsNullOrWhiteSpace(request.Source))
        {
            return BadRequest(new ErrorResponse { Error = "Source (agent name) is required" });
        }

        _logger.LogInformation("Notification from {Source}: {Text}", request.Source, request.Text);

        var notificationId = await _notificationService.CreateNotificationAsync(request.Text, request.Source, ct);

        return Ok(new { success = true, id = notificationId, text = request.Text, source = request.Source });
    }
}

/// <summary>
/// Request model for creating a notification.
/// </summary>
public class CreateNotificationRequest
{
    /// <summary>
    /// Notification text content.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Source agent identifier (e.g., "claude-code", "opencode").
    /// </summary>
    public string? Source { get; set; }
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
