using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using VirtualAssistant.Core.Models;
using VirtualAssistant.Core.Services;

namespace VirtualAssistant.Desktop.Services;

/// <summary>
/// Service for tracking user's desktop context using FocusTracker extension.
/// Uses polling to detect changes and emit events via reactive observable.
/// </summary>
public class DesktopContextService : IDesktopContextService, IAsyncDisposable
{
    private readonly IFocusTrackerService? _focusTracker;
    private readonly ILogger<DesktopContextService> _logger;
    private readonly Subject<DesktopContextChange> _contextChanges = new();
    private readonly Timer? _pollTimer;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private DesktopContext? _lastContext;
    private bool _disposed;

    public DesktopContextService(
        IFocusTrackerService? focusTracker,
        ILogger<DesktopContextService> logger)
    {
        _focusTracker = focusTracker;
        _logger = logger;

        if (_focusTracker != null)
        {
            // Start polling for changes (500ms interval)
            _pollTimer = new Timer(state => _ = PollForChangesAsync(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
            _logger.LogInformation("Desktop context monitoring active via FocusTracker (polling mode)");
        }
        else
        {
            _logger.LogWarning("Desktop monitoring unavailable - FocusTrackerService is null (graceful degradation)");
        }
    }

    public IObservable<DesktopContextChange> ContextChanges => _contextChanges;

    private async Task PollForChangesAsync()
    {
        if (_disposed || _focusTracker == null) return;

        try
        {
            await _lock.WaitAsync();
            try
            {
                var newContext = await _focusTracker.GetCurrentContextAsync();

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
            _logger.LogError(ex, "Error polling for desktop context changes");
        }
    }

    public async Task<DesktopContext> GetCurrentContextAsync(CancellationToken ct = default)
    {
        if (_focusTracker == null)
        {
            _logger.LogWarning("Desktop monitoring unavailable, returning empty context");
            return CreateEmptyContext();
        }

        try
        {
            return await _focusTracker.GetCurrentContextAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current desktop context");
            return CreateEmptyContext();
        }
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_focusTracker != null);
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

        _pollTimer?.Dispose();

        await _lock.WaitAsync();
        try
        {
            _contextChanges.OnCompleted();
            _contextChanges.Dispose();

            if (_focusTracker != null)
                await _focusTracker.DisposeAsync();

            _logger.LogInformation("DesktopContextService disposed");
        }
        finally
        {
            _lock.Release();
        }

        _lock.Dispose();
    }
}
