using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Desktop.Services;

/// <summary>
/// Service for tracking user's desktop context using DesktopMonitorBackgroundService.
/// Subscribes to event-driven D-Bus signals and emits desktop context changes.
/// </summary>
public class DesktopContextService : IDesktopContextService, IAsyncDisposable
{
    private readonly IDesktopMonitorBackgroundService? _monitor;
    private readonly ILogger<DesktopContextService> _logger;
    private readonly Subject<DesktopContextChange> _contextChanges = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IDisposable? _contextSubscription;

    private DesktopContext? _lastContext;
    private bool _disposed;

    public DesktopContextService(
        IDesktopMonitorBackgroundService? monitor,
        ILogger<DesktopContextService> logger)
    {
        _monitor = monitor;
        _logger = logger;

        if (_monitor != null)
        {
            // Subscribe to context updates from BackgroundService (event-driven, no polling!)
            _contextSubscription = _monitor.ContextUpdates.Subscribe(OnContextUpdate);
            _logger.LogInformation("Desktop context monitoring active via DesktopMonitorBackgroundService (event-driven)");
        }
        else
        {
            _logger.LogWarning("Desktop monitoring unavailable - DesktopMonitorBackgroundService is null (graceful degradation)");
        }
    }

    public IObservable<DesktopContextChange> ContextChanges => _contextChanges;

    private void OnContextUpdate(DesktopContext newContext)
    {
        if (_disposed) return;

        try
        {
            _lock.Wait();
            try
            {
                if (_lastContext == null)
                {
                    _lastContext = newContext;
                    return;
                }

                // Detect changes and emit events
                if (_lastContext.CurrentWorkspace != newContext.CurrentWorkspace)
                {
                    var oldContext = _lastContext;
                    var change = new DesktopContextChange(
                        oldContext, newContext, ChangeType.WorkspaceChanged);
                    _logger.LogDebug("Workspace changed: {OldWs} -> {NewWs}",
                        oldContext.CurrentWorkspace, newContext.CurrentWorkspace);
                    _lastContext = newContext;
                    _contextChanges.OnNext(change);
                }
                else if (_lastContext.ActiveApplication != newContext.ActiveApplication)
                {
                    var oldContext = _lastContext;
                    var change = new DesktopContextChange(
                        oldContext, newContext, ChangeType.ApplicationChanged);
                    _logger.LogDebug("Application changed: {OldApp} -> {NewApp}",
                        oldContext.ActiveApplication, newContext.ActiveApplication);
                    _lastContext = newContext;
                    _contextChanges.OnNext(change);
                }
                else if (_lastContext.ActiveWindowTitle != newContext.ActiveWindowTitle)
                {
                    var oldContext = _lastContext;
                    var change = new DesktopContextChange(
                        oldContext, newContext, ChangeType.WindowFocusChanged);
                    _logger.LogDebug("Window focus changed: {OldTitle} -> {NewTitle}",
                        oldContext.ActiveWindowTitle, newContext.ActiveWindowTitle);
                    _lastContext = newContext;
                    _contextChanges.OnNext(change);
                }
            }
            finally
            {
                _lock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing desktop context update");
        }
    }

    public Task<DesktopContext> GetCurrentContextAsync(CancellationToken ct = default)
    {
        if (_monitor == null || !_monitor.IsAvailable)
        {
            _logger.LogWarning("Desktop monitoring unavailable, returning empty context");
            return Task.FromResult(CreateEmptyContext());
        }

        try
        {
            var context = _monitor.CurrentContext ?? CreateEmptyContext();
            return Task.FromResult(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current desktop context");
            return Task.FromResult(CreateEmptyContext());
        }
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_monitor != null && _monitor.IsAvailable);
    }

    private static DesktopContext CreateEmptyContext() => new(
        CurrentWorkspace: 0,
        TotalWorkspaces: 0,
        ActiveWindowTitle: "Unknown",
        ActiveWindowClass: "Unknown",
        ActiveApplication: "Unknown",
        Timestamp: DateTime.UtcNow
    );

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Unsubscribe from BackgroundService events
        _contextSubscription?.Dispose();

        await _lock.WaitAsync();
        try
        {
            _contextChanges.OnCompleted();
            _contextChanges.Dispose();

            _logger.LogInformation("DesktopContextService disposed");
        }
        finally
        {
            _lock.Release();
        }

        _lock.Dispose();
    }
}
