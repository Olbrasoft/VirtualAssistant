using Microsoft.AspNetCore.Mvc;
using Olbrasoft.VoiceAssistant.EdgeTtsWebSocketServer.Models;
using Olbrasoft.VoiceAssistant.EdgeTtsWebSocketServer.Services;
using Olbrasoft.Mediation;
using Olbrasoft.VoiceAssistant.Shared.Data.Queries.SpeechLockQueries;

namespace Olbrasoft.VoiceAssistant.EdgeTtsWebSocketServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SpeechController : ControllerBase
{
    private readonly EdgeTtsService _edgeTtsService;
    private readonly IMediator _mediator;
    private readonly ILogger<SpeechController> _logger;

    public SpeechController(EdgeTtsService edgeTtsService, IMediator mediator, ILogger<SpeechController> logger)
    {
        _edgeTtsService = edgeTtsService;
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("speak")]
    public async Task<ActionResult<SpeechResponse>> Speak([FromBody] SpeechRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new SpeechResponse
            {
                Success = false,
                Message = "Text is required"
            });
        }

        _logger.LogInformation("Speech request: {Text}", request.Text);

        var (success, message, cached) = await _edgeTtsService.SpeakAsync(
            request.Text,
            request.Voice,
            request.Rate,
            request.Volume,
            request.Pitch,
            request.Play
        );

        return new SpeechResponse
        {
            Success = success,
            Message = message,
            Cached = cached
        };
    }

    [HttpDelete("cache")]
    public ActionResult<object> ClearCache()
    {
        var count = _edgeTtsService.ClearCache();
        return Ok(new { message = $"✅ Cleared {count} cached files" });
    }

    /// <summary>
    /// Stops current speech playback immediately.
    /// </summary>
    [HttpPost("stop")]
    public ActionResult<object> Stop()
    {
        _logger.LogInformation("Stop speech request received");
        var stopped = _edgeTtsService.StopSpeaking();
        
        return Ok(new { 
            success = true, 
            stopped = stopped,
            message = stopped ? "✅ Speech stopped" : "ℹ️ Nothing was playing" 
        });
    }

    /// <summary>
    /// Check if TTS can speak (no active speech lock).
    /// Returns canSpeak=false if user is currently recording.
    /// </summary>
    [HttpGet("can-speak")]
    public async Task<ActionResult<object>> CanSpeak()
    {
        try
        {
            var query = new SpeechLockExistsQuery { MaxAgeMinutes = 1 };
            var isLocked = await _mediator.MediateAsync(query);
            
            _logger.LogDebug("CanSpeak check: isLocked={IsLocked}", isLocked);
            
            return Ok(new
            {
                canSpeak = !isLocked,
                isLocked = isLocked,
                message = isLocked ? "🔒 Speech locked - user is recording" : "✅ Ready to speak"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking speech lock");
            // On error, allow speaking (fail open)
            return Ok(new
            {
                canSpeak = true,
                isLocked = false,
                message = "⚠️ Could not check lock, allowing speech"
            });
        }
    }
}
