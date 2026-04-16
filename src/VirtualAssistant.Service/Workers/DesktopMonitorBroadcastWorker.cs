using Microsoft.AspNetCore.SignalR;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Service.Hubs;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;
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
    private readonly IHubContext<DictationHub> _dictationHubContext;
    private readonly IQueryProcessor _queryProcessor;
    private readonly ICliAppDetector _cliAppDetector;
    private readonly object _subscriptionLock = new();
    private readonly SemaphoreSlim _dbLock = new(1, 1);  // Serialize DB operations
    private IDisposable? _subscription;

    // Transient windows that should not trigger prompt changes (e.g., clipboard utilities)
    private static readonly string[] TransientWindowTitles = ["wl-clipboard", "xclip", "xsel"];

    public DesktopMonitorBroadcastWorker(
        ILogger<DesktopMonitorBroadcastWorker> logger,
        IDesktopContextService desktopContextService,
        IHubContext<DesktopMonitorHub> hubContext,
        IHubContext<DictationHub> dictationHubContext,
        IQueryProcessor queryProcessor,
        ICliAppDetector cliAppDetector)
    {
        _logger = logger;
        _desktopContextService = desktopContextService;
        _hubContext = hubContext;
        _dictationHubContext = dictationHubContext;
        _queryProcessor = queryProcessor;
        _cliAppDetector = cliAppDetector;
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
                    // Skip prompt change for transient windows (clipboard utilities, etc.)
                    if (!IsTransientWindow(newContext.ActiveWindowTitle))
                    {
                        await BroadcastPromptChanged(newContext);
                    }
                    else
                    {
                        _logger.LogDebug("Skipping prompt change for transient window: {Title}", newContext.ActiveWindowTitle);
                    }
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

        // Also broadcast to DictationHub for remote control workspace buttons
        await _dictationHubContext.Clients.All.SendAsync("WorkspaceChanged", context.CurrentWorkspace + 1, context.TotalWorkspaces);
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

        // Also broadcast to DictationHub for app launcher button state
        await _dictationHubContext.Clients.All.SendAsync(
            "AppFocusChanged",
            context.ActiveWindowClass ?? ""
        );
    }

    private async Task BroadcastLogMessage(string message)
    {
        _logger.LogDebug("Broadcasting log message: {Message}", message);
        await _hubContext.Clients.All.SendAsync("LogMessage", message);
    }

    private async Task BroadcastPromptChanged(DesktopContext context)
    {
        // Serialize DB operations to avoid DbContext threading issues
        await _dbLock.WaitAsync();
        try
        {
            // Priority 0: Check for CLI apps running in terminals (e.g., Claude Code, OpenCode)
            // This handles cases where CLI apps change the terminal window title
            var cliApp = await _cliAppDetector.DetectCliAppAsync(CancellationToken.None);
            await _dictationHubContext.Clients.All.SendAsync("CliAppChanged", cliApp?.AppName ?? "");
            if (cliApp != null)
            {
                _logger.LogDebug(
                    "CLI app detected in terminal: {AppName} → {Prompt}",
                    cliApp.AppName, cliApp.PromptFileName);

                await _hubContext.Clients.All.SendAsync(
                    "PromptChanged",
                    cliApp.AppName,
                    context.ActiveWindowTitle,
                    cliApp.PromptFileName);
                return;
            }

            // Detect prompt by matching:
            // 1) ApplicationPattern against ActiveApplication (e.g., "antigravity.desktop")
            // 2) AppIdPattern against ActiveWindowTitle (e.g., "Claude Code", "OC |")
            var prompt = await _queryProcessor.ProcessAsync(
                new GetPromptByAppIdPatternQuery(context.ActiveWindowTitle, context.ActiveApplication),
                CancellationToken.None);

            // Fallback to Default if no match
            prompt ??= await _queryProcessor.ProcessAsync(
                new GetDefaultPromptQuery(),
                CancellationToken.None);

            _logger.LogDebug(
                "Broadcasting prompt change: {WindowTitle} ({App}) → {Prompt}",
                context.ActiveWindowTitle, context.ActiveApplication, prompt?.PromptFileName ?? "null");

            // Broadcast prompt change to all connected clients
            // Use context.ActiveWindowTitle for APP ID to match ACTIVE WINDOW display
            await _hubContext.Clients.All.SendAsync(
                "PromptChanged",
                prompt?.ApplicationName ?? "Unknown",  // e.g., "Claude Code"
                context.ActiveWindowTitle,             // SAME as ACTIVE WINDOW field
                prompt?.PromptFileName ?? "DefaultCorrection");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect prompt for window title '{Title}'", context.ActiveWindowTitle);

            // Even on error, broadcast with correct window title
            await _hubContext.Clients.All.SendAsync(
                "PromptChanged",
                "Default",
                context.ActiveWindowTitle,  // SAME as ACTIVE WINDOW field
                "DefaultCorrection");
        }
        finally
        {
            _dbLock.Release();
        }
    }

    /// <summary>
    /// Checks if the window title belongs to a transient utility window (clipboard, etc.)
    /// that should not trigger prompt changes.
    /// </summary>
    private static bool IsTransientWindow(string windowTitle)
    {
        return TransientWindowTitles.Any(t =>
            windowTitle.Equals(t, StringComparison.OrdinalIgnoreCase));
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
