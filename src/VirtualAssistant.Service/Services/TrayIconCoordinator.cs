using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Services;

/// <summary>
/// Coordinates tray icon management for left hand, center (VirtualAssistant), and right hand icons.
/// Implements Single Responsibility Principle - only handles icon coordination.
/// </summary>
public class TrayIconCoordinator : ITrayIconCoordinator, IDisposable
{
    private readonly ITrayIconManager _manager;
    private readonly string _iconsPath;
    private readonly IManualMuteService _muteService;
    private readonly Core.Services.ITrayMenuHandler? _menuHandler;
    private readonly ILogger<TrayIconCoordinator> _logger;

    private Core.Services.ITrayIcon? _leftHandIcon;
    private Core.Services.ITrayIcon? _rightHandIcon;
    private Core.Services.ITrayIcon? _centerIcon;

    private string _currentLeftHandIcon = "default-left-hand.svg";
    private string _currentRightHandIcon = "default-right-hand.svg";
    private string _currentCenterIconPath = string.Empty;
    private string _currentTooltip = "VirtualAssistant - poslouchám";

    private bool _disposed;

    public TrayIconCoordinator(
        ITrayIconManager manager,
        string iconsPath,
        IManualMuteService muteService,
        ILogger<TrayIconCoordinator> logger,
        Core.Services.ITrayMenuHandler? menuHandler = null)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _iconsPath = iconsPath ?? throw new ArgumentNullException(nameof(iconsPath));
        _muteService = muteService ?? throw new ArgumentNullException(nameof(muteService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _menuHandler = menuHandler;
    }

    /// <summary>
    /// Initializes all three tray icons (left hand, center, right hand).
    /// </summary>
    public async Task InitializeIconsAsync()
    {
        try
        {
            // Create left hand icon (appears first on startup)
            var leftHandPath = Path.Combine(_iconsPath, "hands", _currentLeftHandIcon);
            _leftHandIcon = await _manager.CreateIconAsync(
                "virtual-assistant-left-hand",
                leftHandPath,
                "VirtualAssistant - Left Hand",
                null);

            if (_leftHandIcon != null)
            {
                _logger.LogInformation("Left hand icon initialized: {Icon}", _currentLeftHandIcon);
            }

            // Determine initial center icon based on mute state
            var iconFileName = _muteService.IsMuted ? "virtual-assistant-muted.svg" : "virtual-assistant-listening.svg";
            var iconPath = Path.Combine(_iconsPath, iconFileName);

            _currentCenterIconPath = iconPath;

            // Create center tray icon with menu handler
            _centerIcon = await _manager.CreateIconAsync(
                "virtual-assistant-service",
                iconPath,
                _currentTooltip,
                _menuHandler);

            if (_centerIcon != null)
            {
                _logger.LogInformation("VirtualAssistant center icon initialized with context menu");
            }

            // Create right hand icon (appears when VA icon is displayed)
            var rightHandPath = Path.Combine(_iconsPath, "hands", _currentRightHandIcon);
            _rightHandIcon = await _manager.CreateIconAsync(
                "virtual-assistant-right-hand",
                rightHandPath,
                "VirtualAssistant - Right Hand",
                null);

            if (_rightHandIcon != null)
            {
                _logger.LogInformation("Right hand icon initialized: {Icon}", _currentRightHandIcon);
            }

            _logger.LogInformation("All tray icons initialized (left hand, VirtualAssistant, right hand)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize tray icons");
            throw;
        }
    }

    /// <summary>
    /// Updates the center icon based on mute state.
    /// </summary>
    /// <param name="isMuted">Whether the assistant is muted</param>
    public void UpdateCenterIcon(bool isMuted)
    {
        try
        {
            if (_centerIcon == null)
            {
                _logger.LogWarning("Center icon not initialized");
                return;
            }

            var iconFileName = isMuted ? "virtual-assistant-muted.svg" : "virtual-assistant-listening.svg";
            var iconPath = Path.Combine(_iconsPath, iconFileName);
            _currentCenterIconPath = iconPath;

            _centerIcon.SetIcon(iconPath, _currentTooltip);
            _logger.LogDebug("Center icon updated to reflect mute state: {IsMuted}", isMuted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update center icon for mute state");
        }
    }

    /// <summary>
    /// Sets the left hand icon to the specified icon file.
    /// </summary>
    /// <param name="iconFileName">Icon file name (e.g., "default-left-hand.svg")</param>
    public void SetLeftHandIcon(string iconFileName)
    {
        try
        {
            if (_leftHandIcon == null)
            {
                _logger.LogWarning("Left hand icon not initialized");
                return;
            }

            var iconPath = Path.Combine(_iconsPath, "hands", iconFileName);
            _leftHandIcon.SetIcon(iconPath, "VirtualAssistant - Left Hand");
            _currentLeftHandIcon = iconFileName;
            _logger.LogDebug("Left hand icon changed to: {Icon}", iconFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set left hand icon: {Icon}", iconFileName);
        }
    }

    /// <summary>
    /// Sets the right hand icon to the specified icon file.
    /// </summary>
    /// <param name="iconFileName">Icon file name (e.g., "default-right-hand.svg")</param>
    public void SetRightHandIcon(string iconFileName)
    {
        try
        {
            if (_rightHandIcon == null)
            {
                _logger.LogWarning("Right hand icon not initialized");
                return;
            }

            var iconPath = Path.Combine(_iconsPath, "hands", iconFileName);
            _rightHandIcon.SetIcon(iconPath, "VirtualAssistant - Right Hand");
            _currentRightHandIcon = iconFileName;
            _logger.LogDebug("Right hand icon changed to: {Icon}", iconFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set right hand icon: {Icon}", iconFileName);
        }
    }

    /// <summary>
    /// Releases resources used by the tray icon coordinator.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Remove left hand icon
        if (_leftHandIcon != null)
        {
            _manager.RemoveIcon("virtual-assistant-left-hand");
            _leftHandIcon = null;
        }

        // Remove right hand icon
        if (_rightHandIcon != null)
        {
            _manager.RemoveIcon("virtual-assistant-right-hand");
            _rightHandIcon = null;
        }

        // Remove center icon
        if (_centerIcon != null)
        {
            _manager.RemoveIcon("virtual-assistant-service");
            _centerIcon = null;
        }

        _logger.LogInformation("TrayIconCoordinator disposed");
    }
}
