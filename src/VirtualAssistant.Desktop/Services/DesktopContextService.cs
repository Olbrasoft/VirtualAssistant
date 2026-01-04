using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using Olbrasoft.LinuxDesktop.Core.Services;
using VirtualAssistant.Core.Models;
using VirtualAssistant.Core.Services;

namespace VirtualAssistant.Desktop.Services;

/// <summary>
/// Service for tracking user's desktop context with caching and reactive updates.
/// </summary>
public class DesktopContextService : IDesktopContextService, IDisposable
{
    private readonly IWindowService? _windowService;
    private readonly IWorkspaceService? _workspaceService;
    private readonly ILogger<DesktopContextService> _logger;
    private readonly Subject<DesktopContextChange> _contextChanges = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    private DesktopContext? _cachedContext;
    private Timer? _pollTimer;
    private bool _disposed;

    public DesktopContextService(
        IWindowService? windowService,
        IWorkspaceService? workspaceService,
        ILogger<DesktopContextService> logger)
    {
        _windowService = windowService;
        _workspaceService = workspaceService;
        _logger = logger;

        // Start polling for changes (every 500ms)
        if (_windowService != null && _workspaceService != null)
        {
            _pollTimer = new Timer(PollForChanges, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
        }
        else
        {
            _logger.LogWarning("Desktop monitoring unavailable - IWindowService or IWorkspaceService is null");
        }
    }

    public IObservable<DesktopContextChange> ContextChanges => _contextChanges;

    public async Task<DesktopContext> GetCurrentContextAsync(CancellationToken ct = default)
    {
        if (_windowService == null || _workspaceService == null)
        {
            _logger.LogWarning("Desktop monitoring unavailable, returning cached context");
            return _cachedContext ?? CreateEmptyContext();
        }

        try
        {
            var focusedWindow = await _windowService.GetFocusedWindowAsync(ct);
            var currentWorkspace = await _workspaceService.GetActiveWorkspaceAsync(ct);
            var totalWorkspaces = await _workspaceService.GetWorkspaceCountAsync(ct);

            var context = new DesktopContext(
                CurrentWorkspace: currentWorkspace,
                TotalWorkspaces: totalWorkspaces,
                ActiveWindowTitle: focusedWindow?.Title ?? "Unknown",
                ActiveWindowClass: focusedWindow?.WmClass ?? "Unknown",
                ActiveApplication: focusedWindow?.WmClass ?? "Unknown",
                Timestamp: DateTime.UtcNow
            );

            await UpdateCacheAsync(context);
            return context;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current desktop context");
            return _cachedContext ?? CreateEmptyContext();
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (_windowService == null || _workspaceService == null)
            return false;

        try
        {
            // Test D-Bus connection
            _ = await _workspaceService.GetActiveWorkspaceAsync(ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async void PollForChanges(object? state)
    {
        if (_disposed)
            return;

        try
        {
            var newContext = await GetCurrentContextAsync();

            if (_cachedContext != null && HasSignificantChange(_cachedContext, newContext))
            {
                var changeType = DetermineChangeType(_cachedContext, newContext);

                _logger.LogInformation(
                    "Desktop context changed: {Type} - Workspace {OldWs}→{NewWs}, App {OldApp}→{NewApp}",
                    changeType,
                    _cachedContext.CurrentWorkspace,
                    newContext.CurrentWorkspace,
                    _cachedContext.ActiveApplication,
                    newContext.ActiveApplication
                );

                _contextChanges.OnNext(new DesktopContextChange(
                    _cachedContext,
                    newContext,
                    changeType
                ));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling for desktop context changes");
        }
    }

    private async Task UpdateCacheAsync(DesktopContext context)
    {
        await _cacheLock.WaitAsync();
        try
        {
            _cachedContext = context;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private static bool HasSignificantChange(DesktopContext old, DesktopContext @new)
    {
        return old.CurrentWorkspace != @new.CurrentWorkspace
            || old.ActiveWindowClass != @new.ActiveWindowClass;
    }

    private static ChangeType DetermineChangeType(DesktopContext old, DesktopContext @new)
    {
        if (old.CurrentWorkspace != @new.CurrentWorkspace)
            return ChangeType.WorkspaceChanged;

        if (old.ActiveApplication != @new.ActiveApplication)
            return ChangeType.ApplicationChanged;

        return ChangeType.WindowFocusChanged;
    }

    private static DesktopContext CreateEmptyContext() => new(
        CurrentWorkspace: 0,
        TotalWorkspaces: 0,
        ActiveWindowTitle: "Unknown",
        ActiveWindowClass: "Unknown",
        ActiveApplication: "Unknown",
        Timestamp: DateTime.UtcNow
    );

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _pollTimer?.Dispose();
        _contextChanges.Dispose();
        _cacheLock.Dispose();

        GC.SuppressFinalize(this);
    }
}
