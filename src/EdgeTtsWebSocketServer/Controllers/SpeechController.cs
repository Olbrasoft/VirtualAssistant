using Microsoft.AspNetCore.Mvc;
using Olbrasoft.VoiceAssistant.EdgeTtsWebSocketServer.Models;
using Olbrasoft.VoiceAssistant.EdgeTtsWebSocketServer.Services;

namespace Olbrasoft.VoiceAssistant.EdgeTtsWebSocketServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SpeechController : ControllerBase
{
    private readonly EdgeTtsService _edgeTtsService;
    private readonly ILogger<SpeechController> _logger;

    public SpeechController(EdgeTtsService edgeTtsService, ILogger<SpeechController> logger)
    {
        _edgeTtsService = edgeTtsService;
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

}
