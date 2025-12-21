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
        string iconsPath,
        int logViewerPort = 5053,
        ITrayMenuHandler? menuHandler = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _muteService = muteService ?? throw new ArgumentNullException(nameof(muteService));
        _iconsPath = iconsPath;
        _logViewerPort = logViewerPort;
        _menuHandler = menuHandler;

        // Subscribe to mute state changes
        _muteService.MuteStateChanged += OnMuteStateChanged;

        // Wire up menu handler events if provided
        if (_menuHandler is VirtualAssistantDBusMenuHandler handler)
        {
            handler.OnQuitRequested += () => OnQuitRequested?.Invoke();
            handler.OnMuteToggleRequested += HandleMuteToggle;
            handler.OnShowLogsRequested += HandleShowLogs;
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
            var iconFileName = _muteService.IsMuted ? "virtual-assistant-muted.svg" : "virtual-assistant-active.svg";
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
            var iconFileName = isMuted ? "virtual-assistant-muted.svg" : "virtual-assistant-active.svg";
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

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Unsubscribe from mute service
        _muteService.MuteStateChanged -= OnMuteStateChanged;

        // Remove tray icon
        if (_trayIcon != null)
        {
            _manager.RemoveIcon("virtual-assistant-service");
            _trayIcon = null;
        }

        _logger.LogInformation("VirtualAssistant tray service disposed");
    }
}
