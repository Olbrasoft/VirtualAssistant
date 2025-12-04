using Microsoft.AspNetCore.SignalR;
using Olbrasoft.VirtualAssistant.PushToTalk.Service.Services;

namespace Olbrasoft.VirtualAssistant.PushToTalk.Service.Hubs;

/// <summary>
/// SignalR hub for real-time Push-to-Talk dictation events.
/// Provides WebSocket endpoint for clients to receive PTT notifications.
/// </summary>
public class PttHub : Hub
{
    private readonly ILogger<PttHub> _logger;
    private readonly ManualMuteService _manualMuteService;
    private readonly IPttNotifier _pttNotifier;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="PttHub"/> class.
    /// </summary>
    /// <param name="logger">Logger for connection events.</param>
    /// <param name="manualMuteService">Service for managing manual mute state.</param>
    /// <param name="pttNotifier">Notifier for broadcasting PTT events.</param>
    public PttHub(
        ILogger<PttHub> logger, 
        ManualMuteService manualMuteService,
        IPttNotifier pttNotifier)
    {
        _logger = logger;
        _manualMuteService = manualMuteService;
        _pttNotifier = pttNotifier;
    }
    
    /// <summary>
    /// Called when a client connects to the hub.
    /// Sends a connection confirmation message to the client.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }
    
    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    /// <param name="exception">Exception that caused the disconnection, if any.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
    
    /// <summary>
    /// Allows a client to subscribe to PTT events with a custom name.
    /// </summary>
    /// <param name="clientName">Name of the subscribing client.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Subscribe(string clientName)
    {
        _logger.LogInformation("Client {ClientName} subscribed", clientName);
        await Clients.Caller.SendAsync("Subscribed", clientName);
    }
    
    /// <summary>
    /// Toggles the manual mute state and broadcasts the change to all clients.
    /// Called from clients (e.g., tray menu) to toggle mute.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ToggleManualMute()
    {
        _logger.LogInformation("Client {ConnectionId} requested ToggleManualMute", Context.ConnectionId);
        
        // Toggle internal state and get new value
        var newMuteState = _manualMuteService.Toggle();
        
        _logger.LogInformation("ManualMute toggled to: {State}", newMuteState ? "MUTED" : "UNMUTED");
        
        // Broadcast to all clients
        await _pttNotifier.NotifyManualMuteChangedAsync(newMuteState);
    }
    
    /// <summary>
    /// Gets the current manual mute state (ScrollLock LED state).
    /// </summary>
    /// <returns>True if muted (ScrollLock ON), false if unmuted.</returns>
    public Task<bool> GetManualMuteState()
    {
        return Task.FromResult(_manualMuteService.IsMuted);
    }
}
