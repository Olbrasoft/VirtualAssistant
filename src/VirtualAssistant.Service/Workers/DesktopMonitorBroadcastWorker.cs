using Microsoft.AspNetCore.SignalR;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Service.Hubs;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Data.Queries.PromptQueries;

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
    private readonly IQueryProcessor _queryProcessor;
    private readonly object _subscriptionLock = new();
    private IDisposable? _subscription;

    public DesktopMonitorBroadcastWorker(
        ILogger<DesktopMonitorBroadcastWorker> logger,
        IDesktopContextService desktopContextService,
        IHubContext<DesktopMonitorHub> hubContext,
        IQueryProcessor queryProcessor)
    {
        _logger = logger;
        _desktopContextService = desktopContextService;
        _hubContext = hubContext;
        _queryProcessor = queryProcessor;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Desktop Monitor broadcast worker starting...");

        // Subscribe to desktop context changes
        lock (_subscriptionLock)
        {
            _subscription = _desktopContextService.ContextChanges.Subscribe(
                change => _ = OnContextChanged(change),
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
                    await BroadcastPromptChanged(newContext.ActiveApplication);  // NEW: Broadcast prompt change
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
        await _hubContext.Clients.All.SendAsync("WorkspaceChanged", context.CurrentWorkspace + 1, context.TotalWorkspaces);
    }

    private async Task BroadcastFocusChanged(DesktopContext context)
    {
        _logger.LogDebug("Broadcasting focus change: {App} ({Class})", context.ActiveApplication, context.ActiveWindowClass);
        await _hubContext.Clients.All.SendAsync(
            "FocusChanged",
            context.ActiveWindowTitle,
            context.ActiveApplication,
            context.ActiveWindowClass
        );
    }

    private async Task BroadcastLogMessage(string message)
    {
        _logger.LogDebug("Broadcasting log message: {Message}", message);
        await _hubContext.Clients.All.SendAsync("LogMessage", message);
    }

    private async Task BroadcastPromptChanged(string activeApplication)
    {
        try
        {
            // Detect prompt for current application (reuses queries from #582)
            var prompt = await _queryProcessor.ProcessAsync(
                new GetPromptByAppIdPatternQuery(activeApplication),
                CancellationToken.None);

            // Fallback to Default if no match
            prompt ??= await _queryProcessor.ProcessAsync(
                new GetDefaultPromptQuery(),
                CancellationToken.None);

            _logger.LogDebug(
                "Broadcasting prompt change: {AppName} (ID: {AppId}) → {Prompt}",
                prompt.ApplicationName, activeApplication, prompt.PromptFileName);

            // Broadcast prompt change to all connected clients
            await _hubContext.Clients.All.SendAsync(
                "PromptChanged",
                prompt.ApplicationName,        // e.g., "Claude Code"
                activeApplication,             // e.g., "code"
                prompt.PromptFileName);        // e.g., "ClaudeCodeCorrection.md"
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect prompt for application '{App}'", activeApplication);
        }
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
