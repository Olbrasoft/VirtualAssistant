using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Olbrasoft.SystemTray.Linux;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Tray;

/// <summary>
/// VirtualAssistant-specific tray icon service using SystemTray.Linux package.
/// </summary>
public class VirtualAssistantTrayService : IDisposable
{
    private readonly ILogger<VirtualAssistantTrayService> _logger;
    private readonly TrayIconManager _manager;
    private readonly string _iconsPath;
    private readonly IManualMuteService _muteService;
    private readonly IDependentServiceManager _dependentServiceManager;
    private readonly int _logViewerPort;
    private readonly ITrayMenuHandler? _menuHandler;
    private ITrayIcon? _trayIcon;
    private bool _disposed;

    // Track current icon state
    private string _currentIconPath = string.Empty;
    private string _currentTooltip = string.Empty;

    /// <summary>
    /// Event fired when user requests to quit the application.
    /// </summary>
    public event Action? OnQuitRequested;

    public VirtualAssistantTrayService(
        ILogger<VirtualAssistantTrayService> logger,
        TrayIconManager manager,
        IManualMuteService muteService,
        IDependentServiceManager dependentServiceManager,
        string iconsPath,
        int logViewerPort = 5053,
        ITrayMenuHandler? menuHandler = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _muteService = muteService ?? throw new ArgumentNullException(nameof(muteService));
        _dependentServiceManager = dependentServiceManager ?? throw new ArgumentNullException(nameof(dependentServiceManager));
        _iconsPath = iconsPath;
        _logViewerPort = logViewerPort;
        _menuHandler = menuHandler;

        // Subscribe to mute state changes
        _muteService.MuteStateChanged += OnMuteStateChanged;

        // Subscribe to service status changes
        _dependentServiceManager.ServiceStatusChanged += OnServiceStatusChanged;

        // Wire up menu handler events if provided
        if (_menuHandler is VirtualAssistantDBusMenuHandler handler)
        {
            handler.OnQuitRequested += () => OnQuitRequested?.Invoke();
            handler.OnMuteToggleRequested += HandleMuteToggle;
            handler.OnShowLogsRequested += HandleShowLogs;
            handler.OnRefreshServiceStatusRequested += HandleRefreshServiceStatus;
            handler.OnToggleServiceRequested += HandleToggleService;
            _logger.LogDebug("Menu handler events wired up successfully");
        }
        else
        {
            _logger.LogWarning("Menu handler is not VirtualAssistantDBusMenuHandler type: {Type}", _menuHandler?.GetType().FullName ?? "null");
        }
    }

    /// <summary>
    /// Initializes and shows the tray icon.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            // Determine initial icon based on mute state
            var iconFileName = _muteService.IsMuted ? "virtual-assistant-muted.svg" : "virtual-assistant-listening.svg";
            var iconPath = Path.Combine(_iconsPath, iconFileName);
            var tooltip = "VirtualAssistant - poslouchám";

            _currentIconPath = iconPath;
            _currentTooltip = tooltip;

            // Create tray icon with menu handler
            _trayIcon = await _manager.CreateIconAsync("virtual-assistant-service", iconPath, tooltip, _menuHandler);

            if (_trayIcon != null)
            {
                _logger.LogInformation("VirtualAssistant tray icon initialized with context menu");

                // Update menu handler with initial mute state
                if (_menuHandler is VirtualAssistantDBusMenuHandler handler)
                {
                    handler.UpdateMuteState(_muteService.IsMuted);

                    // Update menu handler with initial service status
                    var servicesStatus = _dependentServiceManager.GetServicesStatus();
                    foreach (var (serviceName, isRunning) in servicesStatus)
                    {
                        handler.UpdateServiceStatus(serviceName, isRunning);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize tray icon");
            throw;
        }
    }

    private void OnMuteStateChanged(object? sender, bool isMuted)
    {
        // Update icon
        try
        {
            if (_trayIcon == null)
                return;

            // Update icon
            var iconFileName = isMuted ? "virtual-assistant-muted.svg" : "virtual-assistant-listening.svg";
            var iconPath = Path.Combine(_iconsPath, iconFileName);
            _currentIconPath = iconPath;

            _trayIcon.SetIcon(iconPath, _currentTooltip);

            // Update menu handler mute state
            if (_menuHandler is VirtualAssistantDBusMenuHandler handler)
            {
                handler.UpdateMuteState(isMuted);
            }

            _logger.LogDebug("Tray icon updated to reflect mute state: {IsMuted}", isMuted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update tray icon for mute state");
        }
    }

    private void OnServiceStatusChanged(object? sender, ServiceStatusChangedEventArgs e)
    {
        try
        {
            // Update menu handler with service status
            if (_menuHandler is VirtualAssistantDBusMenuHandler handler)
            {
                handler.UpdateServiceStatus(e.ServiceName, e.IsRunning);
            }

            _logger.LogDebug("Tray menu updated to reflect service status: {ServiceName} = {IsRunning}",
                e.ServiceName, e.IsRunning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update tray menu for service status");
        }
    }

    /// <summary>
    /// Handles mute toggle request from menu.
    /// </summary>
    private void HandleMuteToggle()
    {
        try
        {
            _muteService.Toggle();
            _logger.LogInformation("Mute toggled via tray menu to: {IsMuted}", _muteService.IsMuted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle mute from tray menu");
        }
    }

    /// <summary>
    /// Handles show logs request from menu.
    /// Opens browser to logs viewer.
    /// </summary>
    private void HandleShowLogs()
    {
        try
        {
            var logsUrl = $"http://localhost:{_logViewerPort}";
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = logsUrl,
                    UseShellExecute = true
                }
            };
            process.Start();
            _logger.LogInformation("Opened logs viewer at {Url}", logsUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open logs viewer");
        }
    }

    /// <summary>
    /// Handles refresh service status request from menu.
    /// Checks if TextToSpeech.Service is running and updates menu.
    /// </summary>
    private async void HandleRefreshServiceStatus()
    {
        try
        {
            _logger.LogInformation("Refreshing TextToSpeech.Service status via tray menu");
            await _dependentServiceManager.RefreshServiceStatusAsync("TextToSpeech.Service");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh service status from tray menu");
        }
    }

    /// <summary>
    /// Handles toggle service request from menu.
    /// Starts or stops TextToSpeech.Service based on current state.
    /// </summary>
    private async void HandleToggleService()
    {
        _logger.LogInformation("HandleToggleService called");
        try
        {
            var servicesStatus = _dependentServiceManager.GetServicesStatus();
            var isRunning = servicesStatus.TryGetValue("TextToSpeech.Service", out var status) && status;
            _logger.LogDebug("Service status: isRunning={IsRunning}", isRunning);

            if (isRunning)
            {
                _logger.LogInformation("Stopping TextToSpeech.Service via tray menu");
                await _dependentServiceManager.StopServiceAsync("TextToSpeech.Service");
            }
            else
            {
                _logger.LogInformation("Starting TextToSpeech.Service via tray menu");
                await _dependentServiceManager.StartServiceAsync("TextToSpeech.Service");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle service from tray menu");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Unsubscribe from mute service
        _muteService.MuteStateChanged -= OnMuteStateChanged;

        // Unsubscribe from dependent service manager
        _dependentServiceManager.ServiceStatusChanged -= OnServiceStatusChanged;

        // Remove tray icon
        if (_trayIcon != null)
        {
            _manager.RemoveIcon("virtual-assistant-service");
            _trayIcon = null;
        }

        _logger.LogInformation("VirtualAssistant tray service disposed");
    }
}
