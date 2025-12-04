using Microsoft.AspNetCore.SignalR;
using Olbrasoft.VirtualAssistant.PushToTalk.Service.Hubs;
using Olbrasoft.VirtualAssistant.PushToTalk.Service.Models;

namespace Olbrasoft.VirtualAssistant.PushToTalk.Service.Services;

/// <summary>
/// Implementation of PTT notifier service using SignalR.
/// Sends Push-to-Talk events to all connected clients via WebSocket.
/// </summary>
public class PttNotifier : IPttNotifier
{
    private readonly IHubContext<PttHub> _hubContext;
    private readonly ILogger<PttNotifier> _logger;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="PttNotifier"/> class.
    /// </summary>
    /// <param name="hubContext">SignalR hub context for sending messages to clients.</param>
    /// <param name="logger">Logger for broadcast operations.</param>
    public PttNotifier(
        IHubContext<PttHub> hubContext,
        ILogger<PttNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }
    
    /// <inheritdoc />
    public async Task NotifyRecordingStartedAsync()
    {
        var pttEvent = new PttEvent
        {
            EventType = PttEventType.RecordingStarted
        };
        
        _logger.LogDebug("Broadcasting RecordingStarted event");
        await _hubContext.Clients.All.SendAsync("PttEvent", pttEvent);
    }
    
    /// <inheritdoc />
    public async Task NotifyRecordingStoppedAsync(double durationSeconds)
    {
        var pttEvent = new PttEvent
        {
            EventType = PttEventType.RecordingStopped,
            DurationSeconds = durationSeconds
        };
        
        _logger.LogDebug("Broadcasting RecordingStopped event (duration: {Duration:F2}s)", durationSeconds);
        await _hubContext.Clients.All.SendAsync("PttEvent", pttEvent);
    }
    
    /// <inheritdoc />
    public async Task NotifyTranscriptionStartedAsync()
    {
        var pttEvent = new PttEvent
        {
            EventType = PttEventType.TranscriptionStarted
        };
        
        _logger.LogDebug("Broadcasting TranscriptionStarted event");
        await _hubContext.Clients.All.SendAsync("PttEvent", pttEvent);
    }
    
    /// <inheritdoc />
    public async Task NotifyTranscriptionCompletedAsync(string text, float confidence)
    {
        var pttEvent = new PttEvent
        {
            EventType = PttEventType.TranscriptionCompleted,
            Text = text,
            Confidence = confidence
        };
        
        _logger.LogDebug("Broadcasting TranscriptionCompleted event: {Text}", text);
        await _hubContext.Clients.All.SendAsync("PttEvent", pttEvent);
    }
    
    /// <inheritdoc />
    public async Task NotifyTranscriptionFailedAsync(string errorMessage)
    {
        var pttEvent = new PttEvent
        {
            EventType = PttEventType.TranscriptionFailed,
            ErrorMessage = errorMessage
        };
        
        _logger.LogWarning("Broadcasting TranscriptionFailed event: {Error}", errorMessage);
        await _hubContext.Clients.All.SendAsync("PttEvent", pttEvent);
    }
    
    /// <inheritdoc />
    public async Task NotifyManualMuteChangedAsync(bool isMuted)
    {
        var pttEvent = new PttEvent
        {
            EventType = isMuted ? PttEventType.ManualMuteOn : PttEventType.ManualMuteOff
        };
        
        _logger.LogInformation("Broadcasting {EventType} event (ScrollLock: {State})", 
            pttEvent.EventType, isMuted ? "ON" : "OFF");
        await _hubContext.Clients.All.SendAsync("PttEvent", pttEvent);
    }
}
