using Microsoft.Extensions.Logging;
using Tmds.DBus;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Desktop.DBus;

namespace Olbrasoft.VirtualAssistant.Desktop.Services;

/// <summary>
/// Service for tracking desktop state via focus-tracker GNOME extension.
/// Reads properties from org.olbrasoft.Desktop D-Bus service.
/// </summary>
public class FocusTrackerService : IFocusTrackerService
{
    private readonly ILogger<FocusTrackerService> _logger;
    private Connection? _connection;
    private IDesktopState? _desktopState;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;
    private bool _initializationFailed;
    private bool _disposed;

    private const string ServiceName = "org.olbrasoft.Desktop";
    private const string ObjectPath = "/org/olbrasoft/Desktop";

    public FocusTrackerService(ILogger<FocusTrackerService> logger)
    {
        _logger = logger;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;
        if (_initializationFailed)
        {
            throw new InvalidOperationException(
                "FocusTrackerService initialization previously failed - focus-tracker extension may not be available");
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;
            if (_initializationFailed)
            {
                throw new InvalidOperationException(
                    "FocusTrackerService initialization previously failed - focus-tracker extension may not be available");
            }

            _connection = new Connection(Address.Session!);
            await _connection.ConnectAsync();

            _desktopState = _connection.CreateProxy<IDesktopState>(
                ServiceName,
                new ObjectPath(ObjectPath)
            );

            // Verify focus-tracker extension is available
            await _desktopState.GetAsync<int>("CurrentWorkspace");

            _initialized = true;
            _logger.LogInformation("FocusTracker service initialized successfully");
        }
        catch (Exception ex)
        {
            _connection?.Dispose();
            _connection = null;
            _desktopState = null;
            _initializationFailed = true;
            _logger.LogWarning(ex, "Failed to initialize FocusTrackerService - focus-tracker extension may not be available");
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<DesktopContext> GetCurrentContextAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (_desktopState == null)
            throw new InvalidOperationException("Desktop state proxy not initialized");

        try
        {
            var currentWorkspace = await _desktopState.GetAsync<int>("CurrentWorkspace");
            var totalWorkspaces = await _desktopState.GetAsync<int>("TotalWorkspaces");
            var activeWindow = await _desktopState.GetAsync<string>("ActiveWindow");
            var activeApp = await _desktopState.GetAsync<string>("ActiveApplication");

            return new DesktopContext(
                CurrentWorkspace: currentWorkspace,
                TotalWorkspaces: totalWorkspaces,
                ActiveWindowTitle: activeWindow,
                ActiveWindowClass: activeApp,
                ActiveApplication: activeApp,
                Timestamp: DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get current desktop context from focus-tracker");
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _initLock.WaitAsync();
        try
        {
            _desktopState = null;
            _connection?.Dispose();
            _logger.LogInformation("FocusTrackerService disposed");
        }
        finally
        {
            _initLock.Release();
            _initLock.Dispose();
        }
    }
}
