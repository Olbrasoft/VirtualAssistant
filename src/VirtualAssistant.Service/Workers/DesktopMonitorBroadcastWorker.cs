using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Service.Hubs;
using VirtualAssistant.Core.Models;
using VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Workers;

/// <summary>
/// Background worker that broadcasts desktop context changes to web clients via SignalR.
/// Subscribes to DesktopContextService changes and pushes updates to DesktopMonitorHub.
/// </summary>
public class DesktopMonitorBroadcastWorker : BackgroundService
{
    private readonly ILogger<DesktopMonitorBroadcastWorker> _logger;
    private readonly IDesktopContextService _desktopContextService;
    private readonly IHubContext<DesktopMonitorHub> _hubContext;
    private readonly object _subscriptionLock = new();
    private IDisposable? _subscription;
    private CancellationToken _stoppingToken;

    public DesktopMonitorBroadcastWorker(
        ILogger<DesktopMonitorBroadcastWorker> logger,
        IDesktopContextService desktopContextService,
        IHubContext<DesktopMonitorHub> hubContext)
    {
        _logger = logger;
        _desktopContextService = desktopContextService;
        _hubContext = hubContext;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        _logger.LogInformation("Desktop Monitor broadcast worker starting...");

        // Subscribe to desktop context changes
        // Using async void lambda is safe here because Observable.Subscribe has error handling
        lock (_subscriptionLock)
        {
            _subscription = _desktopContextService.ContextChanges.Subscribe(
                async change =>
                {
                    try
                    {
                        await OnContextChanged(change);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unhandled error in context change handler");
                    }
                },
                error => _logger.LogError(error, "Error in desktop context change stream"),
                () => _logger.LogInformation("Desktop context change stream completed")
            );
        }

        _logger.LogInformation("Desktop Monitor broadcast worker started - listening for context changes");

        return Task.CompletedTask;
    }

    private async Task OnContextChanged(DesktopContextChange change)
    {
        try
        {
            var newContext = change.NewContext;

            // Broadcast change based on type
            switch (change.Type)
            {
                case ChangeType.WorkspaceChanged:
                    await BroadcastWorkspaceChanged(newContext);
                    break;

                case ChangeType.ApplicationChanged:
                case ChangeType.WindowFocusChanged:
                    await BroadcastFocusChanged(newContext);
                    break;
            }

            // Always log the change
            await BroadcastLogMessage($"{change.Type}: {newContext.ActiveApplication} (Workspace {newContext.CurrentWorkspace + 1}/{newContext.TotalWorkspaces})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast desktop context change");
        }
    }

    private async Task BroadcastWorkspaceChanged(DesktopContext context)
    {
        _logger.LogDebug("Broadcasting workspace change: {Index}/{Total}", context.CurrentWorkspace + 1, context.TotalWorkspaces);
        await _hubContext.Clients.All.SendAsync("WorkspaceChanged", context.CurrentWorkspace + 1, context.TotalWorkspaces, _stoppingToken);
    }

    private async Task BroadcastFocusChanged(DesktopContext context)
    {
        _logger.LogDebug("Broadcasting focus change: {App} ({Class})", context.ActiveApplication, context.ActiveWindowClass);
        await _hubContext.Clients.All.SendAsync(
            "FocusChanged",
            context.ActiveWindowTitle,
            context.ActiveApplication,
            context.ActiveWindowClass,
            _stoppingToken
        );
    }

    private async Task BroadcastLogMessage(string message)
    {
        _logger.LogDebug("Broadcasting log message: {Message}", message);
        await _hubContext.Clients.All.SendAsync("LogMessage", message, _stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Desktop Monitor broadcast worker stopping...");
        lock (_subscriptionLock)
        {
            _subscription?.Dispose();
            _subscription = null;
        }
        return base.StopAsync(cancellationToken);
    }
}
