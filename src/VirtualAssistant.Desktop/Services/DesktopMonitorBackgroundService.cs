extern alias TmdsDBusHighLevel;

using System.Reactive.Subjects;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TmdsDBusHighLevel::Tmds.DBus;
using VirtualAssistant.Core.Models;
using VirtualAssistant.Desktop.DBus;

namespace VirtualAssistant.Desktop.Services;

/// <summary>
/// Background service for monitoring desktop state via D-Bus.
/// Uses BackgroundService pattern to maintain stable thread context for D-Bus connection.
/// Subscribes to D-Bus signals (event-driven) and emits events via System.Reactive observables.
/// Based on working implementation from LinuxDesktop.Monitor.Web.
/// </summary>
public class DesktopMonitorBackgroundService : BackgroundService, IDesktopMonitorBackgroundService
{
    private readonly ILogger<DesktopMonitorBackgroundService> _logger;
    private Connection? _connection;
    private IDesktopState? _desktopState;
    private IDisposable? _workspaceSubscription;
    private IDisposable? _focusSubscription;

    private readonly Subject<WorkspaceChangedEventArgs> _workspaceChanges = new();
    private readonly Subject<FocusChangedEventArgs> _focusChanges = new();
    private readonly Subject<DesktopContext> _contextUpdates = new();

    private DesktopContext? _currentContext;
    private bool _isAvailable;

    public DesktopMonitorBackgroundService(ILogger<DesktopMonitorBackgroundService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Observable stream of workspace change events.
    /// </summary>
    public virtual IObservable<WorkspaceChangedEventArgs> WorkspaceChanges => _workspaceChanges;

    /// <summary>
    /// Observable stream of focus change events.
    /// </summary>
    public virtual IObservable<FocusChangedEventArgs> FocusChanges => _focusChanges;

    /// <summary>
    /// Observable stream of complete desktop context updates.
    /// </summary>
    public virtual IObservable<DesktopContext> ContextUpdates => _contextUpdates;

    /// <summary>
    /// Indicates whether desktop monitoring is available (GNOME extension installed).
    /// </summary>
    public virtual bool IsAvailable => _isAvailable;

    /// <summary>
    /// Gets the current desktop context (cached from last update).
    /// Thread-safe via Interlocked read.
    /// </summary>
    public virtual DesktopContext? CurrentContext => Interlocked.CompareExchange(ref _currentContext, null, null);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DesktopMonitorBackgroundService starting...");

        try
        {
            // Connect to D-Bus session bus (CRITICAL: on stable BackgroundService thread)
            _connection = new Connection(Address.Session!);
            await _connection.ConnectAsync();
            _logger.LogInformation("Connected to D-Bus session bus");

            // Create proxy to focus-tracker extension (high-level Tmds.DBus API)
            _desktopState = _connection.CreateProxy<IDesktopState>(
                "org.olbrasoft.Desktop",
                new ObjectPath("/org/olbrasoft/Desktop")
            );
            _logger.LogInformation("Created proxy to org.olbrasoft.Desktop");

            // Read initial state to verify extension is available
            var currentWorkspace = await _desktopState.GetAsync<int>("CurrentWorkspace");
            var totalWorkspaces = await _desktopState.GetAsync<int>("TotalWorkspaces");
            var activeWindow = await _desktopState.GetAsync<string>("ActiveWindow");
            var activeApp = await _desktopState.GetAsync<string>("ActiveApplication");

            _currentContext = new DesktopContext(
                CurrentWorkspace: currentWorkspace,
                TotalWorkspaces: totalWorkspaces,
                ActiveWindowTitle: activeWindow,
                ActiveWindowClass: activeApp,
                ActiveApplication: activeApp,
                Timestamp: DateTime.UtcNow
            );

            _logger.LogInformation(
                "Initial state: Workspace {Workspace}/{Total}, Window: \"{Title}\" ({App})",
                currentWorkspace, totalWorkspaces, activeWindow, activeApp);

            // Subscribe to workspace changes (D-Bus signal - event-driven!)
            _workspaceSubscription = await _desktopState.WatchWorkspaceChangedAsync(args =>
            {
                _logger.LogInformation("Workspace changed: {NewIndex}/{Total}", args.NewIndex, args.TotalWorkspaces);

                // Emit event via System.Reactive
                var eventArgs = new WorkspaceChangedEventArgs(args.NewIndex, args.TotalWorkspaces);
                _workspaceChanges.OnNext(eventArgs);

                // Update cached context (thread-safe via Interlocked.Exchange)
                var current = Interlocked.CompareExchange(ref _currentContext, null, null);
                if (current != null)
                {
                    var updated = current with
                    {
                        CurrentWorkspace = args.NewIndex,
                        TotalWorkspaces = args.TotalWorkspaces,
                        Timestamp = DateTime.UtcNow
                    };
                    Interlocked.Exchange(ref _currentContext, updated);
                    _contextUpdates.OnNext(updated);
                }
            });
            _logger.LogInformation("Subscribed to WorkspaceChanged signal");

            // Subscribe to focus changes (D-Bus signal - event-driven!)
            _focusSubscription = await _desktopState.WatchFocusChangedAsync(args =>
            {
                _logger.LogInformation("Focus changed: \"{Title}\" ({AppId})", args.WindowTitle, args.AppId);

                // Emit event via System.Reactive
                var eventArgs = new FocusChangedEventArgs(args.WindowTitle, args.AppId, args.WmClass);
                _focusChanges.OnNext(eventArgs);

                // Update cached context (thread-safe via Interlocked.Exchange)
                var current = Interlocked.CompareExchange(ref _currentContext, null, null);
                if (current != null)
                {
                    var updated = current with
                    {
                        ActiveWindowTitle = args.WindowTitle,
                        ActiveWindowClass = args.WmClass,
                        ActiveApplication = args.AppId,
                        Timestamp = DateTime.UtcNow
                    };
                    Interlocked.Exchange(ref _currentContext, updated);
                    _contextUpdates.OnNext(updated);
                }
            });
            _logger.LogInformation("Subscribed to FocusChanged signal");

            _isAvailable = true;
            _logger.LogInformation("Desktop monitoring active (event-driven via D-Bus signals)");

            // Keep BackgroundService thread alive (CRITICAL: maintains D-Bus connection and signal subscriptions)
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (DBusException ex)
        {
            _logger.LogWarning(ex,
                "Desktop monitoring unavailable - focus-tracker extension not installed. " +
                "Graceful degradation: service will run without desktop context awareness.");
            _isAvailable = false;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Desktop monitoring stopped (cancellation requested)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DesktopMonitorBackgroundService");
            _isAvailable = false;
            throw;
        }
    }

    public override void Dispose()
    {
        _logger.LogInformation("DesktopMonitorBackgroundService disposing...");

        // Unsubscribe from D-Bus signals
        _workspaceSubscription?.Dispose();
        _focusSubscription?.Dispose();

        // Complete observables
        _workspaceChanges.OnCompleted();
        _workspaceChanges.Dispose();
        _focusChanges.OnCompleted();
        _focusChanges.Dispose();
        _contextUpdates.OnCompleted();
        _contextUpdates.Dispose();

        // Dispose D-Bus connection
        _connection?.Dispose();

        base.Dispose();

        _logger.LogInformation("DesktopMonitorBackgroundService disposed");
    }
}
