using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Olbrasoft.SystemTray.Linux;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Service.Workers;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Voice.StateMachine;

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
    private readonly SpeechToTextServiceManager? _sttServiceManager;
    private readonly MistralProvider? _mistralProvider;
    private readonly IDictationStateMachine? _dictationStateMachine;
    private readonly DictationWorker? _dictationWorker;
    private ITrayIcon? _trayIcon;
    private ITrayIcon? _leftHandIcon;
    private ITrayIcon? _rightHandIcon;
    private bool _disposed;

    // Track current icon state
    private string _currentIconPath = string.Empty;
    private string _currentTooltip = string.Empty;
    private string _currentLeftHandIcon = "default-left-hand.svg";
    private string _currentRightHandIcon = "default-right-hand.svg";

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
        ITrayMenuHandler? menuHandler = null,
        SpeechToTextServiceManager? sttServiceManager = null,
        MistralProvider? mistralProvider = null,
        IDictationStateMachine? dictationStateMachine = null,
        DictationWorker? dictationWorker = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _muteService = muteService ?? throw new ArgumentNullException(nameof(muteService));
        _dependentServiceManager = dependentServiceManager ?? throw new ArgumentNullException(nameof(dependentServiceManager));
        _iconsPath = iconsPath;
        _logViewerPort = logViewerPort;
        _menuHandler = menuHandler;
        _sttServiceManager = sttServiceManager;
        _mistralProvider = mistralProvider;
        _dictationStateMachine = dictationStateMachine;
        _dictationWorker = dictationWorker;

        // Subscribe to mute state changes
        _muteService.MuteStateChanged += OnMuteStateChanged;

        // Subscribe to dictation state changes
        if (_dictationStateMachine != null)
        {
            _dictationStateMachine.StateChanged += OnDictationStateChanged;
        }

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
            handler.OnStartSpeechToTextRequested += HandleStartSpeechToTextService;
            handler.OnStopSpeechToTextRequested += HandleStopSpeechToTextService;
            handler.OnStartLogViewerRequested += HandleStartLogViewerService;
            handler.OnStopLogViewerRequested += HandleStopLogViewerService;
            handler.OnLlmCorrectionToggled += HandleLlmCorrectionToggle;
            handler.OnReloadPromptRequested += HandleReloadPrompt;
            handler.OnDictationToggleRequested += HandleDictationToggle;
            _logger.LogDebug("Menu handler events wired up successfully");
        }
        else
        {
            _logger.LogWarning("Menu handler is not VirtualAssistantDBusMenuHandler type: {Type}", _menuHandler?.GetType().FullName ?? "null");
        }
    }

    /// <summary>
    /// Initializes and shows the tray icons (left hand, VirtualAssistant, right hand).
    /// </summary>
    public async Task InitializeAsync()
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
            var tooltip = "VirtualAssistant - poslouchám";

            _currentIconPath = iconPath;
            _currentTooltip = tooltip;

            // Create center tray icon with menu handler
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

                    // Refresh SpeechToText status
                    await RefreshSpeechToTextStatus();

                    // Refresh log-viewer status
                    await RefreshLogViewerStatus();
                }
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

    /// <summary>
    /// Handles start SpeechToText service request from menu.
    /// </summary>
    private async void HandleStartSpeechToTextService()
    {
        if (_sttServiceManager == null)
        {
            _logger.LogWarning("SpeechToTextServiceManager not available");
            return;
        }

        try
        {
            _logger.LogInformation("Starting SpeechToText.Service via tray menu");
            var success = await _sttServiceManager.StartAsync();

            if (success)
            {
                // Refresh status after starting
                await RefreshSpeechToTextStatus();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SpeechToText service from tray menu");
        }
    }

    /// <summary>
    /// Handles stop SpeechToText service request from menu.
    /// </summary>
    private async void HandleStopSpeechToTextService()
    {
        if (_sttServiceManager == null)
        {
            _logger.LogWarning("SpeechToTextServiceManager not available");
            return;
        }

        try
        {
            _logger.LogInformation("Stopping SpeechToText.Service via tray menu");
            var success = await _sttServiceManager.StopAsync();

            if (success)
            {
                // Refresh status after stopping
                await RefreshSpeechToTextStatus();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop SpeechToText service from tray menu");
        }
    }

    /// <summary>
    /// Refreshes SpeechToText service status and updates menu.
    /// </summary>
    private async Task RefreshSpeechToTextStatus()
    {
        if (_sttServiceManager == null || _menuHandler is not VirtualAssistantDBusMenuHandler handler)
            return;

        try
        {
            var isRunning = await _sttServiceManager.IsRunningAsync();
            var version = _sttServiceManager.GetVersion();
            handler.UpdateSpeechToTextStatus(isRunning, version);
            _logger.LogDebug("SpeechToText status updated: Running={IsRunning}, Version={Version}",
                isRunning, version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh SpeechToText status");
        }
    }

    /// <summary>
    /// Handles start log-viewer service request from menu.
    /// </summary>
    private async void HandleStartLogViewerService()
    {
        try
        {
            _logger.LogInformation("Starting log-viewer service via tray menu");

            var startInfo = new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = "--user start log-viewer.service",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();

                // Wait a bit for service to start
                await Task.Delay(500);

                // Refresh status
                await RefreshLogViewerStatus();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start log-viewer service from tray menu");
        }
    }

    /// <summary>
    /// Handles stop log-viewer service request from menu.
    /// </summary>
    private async void HandleStopLogViewerService()
    {
        try
        {
            _logger.LogInformation("Stopping log-viewer service via tray menu");

            var startInfo = new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = "--user stop log-viewer.service",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();

                // Wait a bit for service to stop
                await Task.Delay(500);

                // Refresh status
                await RefreshLogViewerStatus();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop log-viewer service from tray menu");
        }
    }

    /// <summary>
    /// Refreshes log-viewer service status and updates menu.
    /// </summary>
    private async Task RefreshLogViewerStatus()
    {
        if (_menuHandler is not VirtualAssistantDBusMenuHandler handler)
            return;

        try
        {
            // Check if service is running
            var startInfo = new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = "--user is-active log-viewer.service",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                var isRunning = process.ExitCode == 0;

                handler.UpdateLogViewerStatus(isRunning);
                _logger.LogDebug("Log-viewer status updated: Running={IsRunning}", isRunning);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh log-viewer status");
        }
    }

    /// <summary>
    /// Handles LLM correction toggle request from menu.
    /// </summary>
    private void HandleLlmCorrectionToggle(bool enabled)
    {
        if (_mistralProvider == null)
        {
            _logger.LogWarning("MistralProvider not available");
            return;
        }

        try
        {
            _logger.LogInformation("Toggling LLM correction to: {Enabled}", enabled);
            _mistralProvider.SetEnabled(enabled);
            _logger.LogInformation("LLM correction successfully {Status}", enabled ? "enabled" : "disabled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle LLM correction");
        }
    }

    /// <summary>
    /// Handles reload LLM prompt request from menu.
    /// Copies prompt from source to deployment location and clears cache.
    /// </summary>
    private void HandleReloadPrompt()
    {
        if (_mistralProvider == null)
        {
            _logger.LogWarning("MistralProvider not available");
            return;
        }

        try
        {
            _logger.LogInformation("Reloading Mistral LLM prompt...");

            // Copy prompt from source to deployment location
            var sourceFile = "/home/jirka/Olbrasoft/VirtualAssistant/src/VirtualAssistant.Voice/Prompts/MistralSystemPrompt.md";
            var deployDir = "/opt/olbrasoft/virtual-assistant/app/Prompts";
            var deployFile = Path.Combine(deployDir, "MistralSystemPrompt.md");

            // Create deployment directory if it doesn't exist
            if (!Directory.Exists(deployDir))
            {
                Directory.CreateDirectory(deployDir);
                _logger.LogInformation("Created deployment directory: {Directory}", deployDir);
            }

            // Copy file from source to deployment location
            if (File.Exists(sourceFile))
            {
                File.Copy(sourceFile, deployFile, overwrite: true);
                _logger.LogInformation("Copied prompt from {Source} to {Destination}", sourceFile, deployFile);
            }
            else
            {
                _logger.LogWarning("Source prompt file not found: {SourceFile}", sourceFile);
            }

            // Clear cache and reload prompt
            _mistralProvider.ReloadPrompt();
            _logger.LogInformation("Mistral LLM prompt reloaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload Mistral prompt");
        }
    }

    private void HandleDictationToggle(bool enabled)
    {
        try
        {
            if (_dictationWorker == null)
            {
                _logger.LogWarning("DictationWorker not available");
                return;
            }

            _logger.LogInformation("Setting dictation enabled: {Enabled}", enabled);
            _dictationWorker.SetDictationEnabled(enabled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle dictation");
        }
    }

    /// <summary>
    /// Handles dictation state changes and updates STT icon and right hand icon accordingly.
    /// </summary>
    private async void OnDictationStateChanged(object? sender, DictationState newState)
    {
        try
        {
            _logger.LogInformation("Dictation state changed to: {State}", newState);

            switch (newState)
            {
                case DictationState.Idle:
                    // Return right hand to default position
                    SetRightHandIcon("default-right-hand.svg");
                    break;

                case DictationState.Recording:
                    // Show right hand holding microphone
                    SetRightHandIcon("holding-up-a-microphone-right-hand.svg");
                    break;

                case DictationState.Transcribing:
                    // Show right hand writing during transcription
                    SetRightHandIcon("writing-right-hand.svg");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update icons for dictation state: {State}", newState);
        }
    }

    /// <summary>
    /// Sets the left hand icon to the specified icon file.
    /// </summary>
    /// <param name="iconFileName">Icon file name (e.g., "default-left-hand.svg", "fist-left-hand.svg")</param>
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
    /// <param name="iconFileName">Icon file name (e.g., "default-right-hand.svg", "fist-right-hand.svg")</param>
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
    /// Releases resources used by the tray service, including removing tray icons and unsubscribing from events.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Unsubscribe from mute service
        _muteService.MuteStateChanged -= OnMuteStateChanged;

        // Unsubscribe from dictation state machine
        if (_dictationStateMachine != null)
        {
            _dictationStateMachine.StateChanged -= OnDictationStateChanged;
        }

        // Unsubscribe from dependent service manager
        _dependentServiceManager.ServiceStatusChanged -= OnServiceStatusChanged;

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

        // Remove tray icon
        if (_trayIcon != null)
        {
            _manager.RemoveIcon("virtual-assistant-service");
            _trayIcon = null;
        }

        _logger.LogInformation("VirtualAssistant tray service disposed");
    }
}
