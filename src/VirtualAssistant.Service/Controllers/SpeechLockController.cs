using Microsoft.AspNetCore.Mvc;
using Olbrasoft.VirtualAssistant.Voice.Services;
using VirtualAssistant.Core.Services;
using VirtualAssistant.Data.Dtos.Common;

namespace Olbrasoft.VirtualAssistant.Service.Controllers;

/// <summary>
/// REST API controller for speech lock management.
/// Allows external services (like SpeechToText) to coordinate TTS playback
/// with recording to prevent audio interference.
/// </summary>
[ApiController]
[Route("api/speech-lock")]
[Produces("application/json")]
public class SpeechLockController : ControllerBase
{
    private readonly ISpeechLockService _speechLockService;
    private readonly IVirtualAssistantSpeaker _speaker;
    private readonly INotificationBatchingService _batchingService;
    private readonly ILogger<SpeechLockController> _logger;

    public SpeechLockController(
        ISpeechLockService speechLockService,
        IVirtualAssistantSpeaker speaker,
        INotificationBatchingService batchingService,
        ILogger<SpeechLockController> logger)
    {
        _speechLockService = speechLockService;
        _speaker = speaker;
        _batchingService = batchingService;
        _logger = logger;
    }

    /// <summary>
    /// Activates speech lock (recording started).
    /// Stops any current TTS playback and prevents new speech until unlocked.
    /// Auto-unlocks after timeout (default 30s) for safety.
    /// </summary>
    /// <param name="request">Optional timeout configuration</param>
    [HttpPost("start")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult Start([FromBody] SpeechLockStartRequest? request = null)
    {
        _logger.LogInformation("Speech lock START requested (timeout: {Timeout}s)",
            request?.TimeoutSeconds ?? 30);

        // Stop current TTS immediately
        _speaker.CancelCurrentSpeech();

        // Activate lock with optional custom timeout
        TimeSpan? timeout = request?.TimeoutSeconds > 0
            ? TimeSpan.FromSeconds(request.TimeoutSeconds)
            : null;

        _speechLockService.Lock(timeout);

        return Ok(new SpeechLockResponse
        {
            Success = true,
            IsLocked = true,
            Message = "Speech lock activated",
            QueueCount = _batchingService.PendingCount
        });
    }

    /// <summary>
    /// Deactivates speech lock (recording stopped).
    /// Unlocks TTS and immediately processes any queued notifications.
    /// </summary>
    [HttpPost("stop")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> Stop()
    {
        _logger.LogInformation("Speech lock STOP requested");

        // Unlock
        _speechLockService.Unlock();

        // Flush queued notifications immediately
        var pendingCount = _batchingService.PendingCount;
        if (pendingCount > 0)
        {
            _logger.LogInformation("Flushing {Count} queued notifications after unlock", pendingCount);
            await _batchingService.FlushAsync();
        }

        return Ok(new SpeechLockResponse
        {
            Success = true,
            IsLocked = false,
            Message = pendingCount > 0
                ? $"Speech lock released, playing {pendingCount} queued message(s)"
                : "Speech lock released",
            QueueCount = _batchingService.PendingCount
        });
    }

    /// <summary>
    /// Gets current speech lock status.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetStatus()
    {
        return Ok(new SpeechLockResponse
        {
            Success = true,
            IsLocked = _speechLockService.IsLocked,
            Message = _speechLockService.IsLocked ? "Speech is locked" : "Speech is unlocked",
            QueueCount = _batchingService.PendingCount,
            IsProcessing = _batchingService.IsProcessing
        });
    }
}

/// <summary>
/// Request model for starting speech lock.
/// </summary>
public class SpeechLockStartRequest
{
    /// <summary>
    /// Optional timeout in seconds after which lock auto-releases.
    /// Default is 30 seconds if not specified.
    /// </summary>
    public int TimeoutSeconds { get; set; }
}

/// <summary>
/// Response model for speech lock operations.
/// </summary>
public class SpeechLockResponse
{
    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Current lock state after operation.
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    /// Human-readable status message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Number of notifications waiting in queue.
    /// </summary>
    public int QueueCount { get; set; }

    /// <summary>
    /// Whether batch processing is currently in progress.
    /// </summary>
    public bool IsProcessing { get; set; }
}
