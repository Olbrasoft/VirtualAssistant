using Microsoft.AspNetCore.SignalR;

namespace Olbrasoft.VirtualAssistant.Service.Hubs;

/// <summary>
/// SignalR hub for broadcasting desktop context changes to web clients.
/// Provides real-time updates for Desktop Monitor UI.
/// </summary>
public class DesktopMonitorHub : Hub
{
    private readonly ILogger<DesktopMonitorHub> _logger;

    public DesktopMonitorHub(ILogger<DesktopMonitorHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Desktop Monitor client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Desktop Monitor client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
