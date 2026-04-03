using Microsoft.AspNetCore.SignalR;
using Olbrasoft.LinuxDesktop.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Keyboard;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using IDesktopContextService = Olbrasoft.VirtualAssistant.Core.Services.IDesktopContextService;

namespace Olbrasoft.VirtualAssistant.Service.Hubs;

/// <summary>
/// SignalR hub for remote dictation control from mobile devices.
/// </summary>
public class DictationHub : Hub
{
    private static readonly HashSet<string> AllowedApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "discord", "ferdium"
    };

    private readonly IDictationService _dictationService;
    private readonly IKeyboardSimulationService _keyboardSimulation;
    private readonly IWindowQueryService _windowQuery;
    private readonly IWindowActionService _windowAction;
    private readonly IDesktopContextService _desktopContext;
    private readonly ILogger<DictationHub> _logger;

    public DictationHub(
        IDictationService dictationService,
        IKeyboardSimulationService keyboardSimulation,
        IWindowQueryService windowQuery,
        IWindowActionService windowAction,
        IDesktopContextService desktopContext,
        ILogger<DictationHub> logger)
    {
        _dictationService = dictationService;
        _keyboardSimulation = keyboardSimulation;
        _windowQuery = windowQuery;
        _windowAction = windowAction;
        _desktopContext = desktopContext;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Gets the WM class of the currently focused window.
    /// </summary>
    public async Task<string> GetFocusedApp()
    {
        var context = await _desktopContext.GetCurrentContextAsync();
        return context.ActiveWindowClass ?? "";
    }

    /// <summary>
    /// Closes the focused window if it matches the given WM class (sends Alt+F4).
    /// </summary>
    public async Task<bool> CloseApp(string wmClass)
    {
        wmClass = wmClass?.Trim() ?? "";
        if (wmClass.Length == 0 || !AllowedApps.Contains(wmClass))
        {
            _logger.LogWarning("CloseApp rejected: '{WmClass}' is not in allowlist", wmClass);
            return false;
        }

        try
        {
            // Verify the focused window matches the requested app
            var context = await _desktopContext.GetCurrentContextAsync();
            if (!string.Equals(context.ActiveWindowClass, wmClass, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("CloseApp '{WmClass}' rejected: focused window is '{Focused}'",
                    wmClass, context.ActiveWindowClass);
                return false;
            }

            _logger.LogInformation("CloseApp '{WmClass}' from client {ConnectionId}", wmClass, Context.ConnectionId);
            await _keyboardSimulation.SendKeyAsync("alt+F4");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CloseApp '{WmClass}' failed", wmClass);
            return false;
        }
    }

    /// <summary>
    /// Gets the current dictation status.
    /// </summary>
    public Task<StatusResponse> GetStatus()
    {
        return Task.FromResult(new StatusResponse
        {
            IsRecording = _dictationService.State == DictationState.Recording,
            IsTranscribing = _dictationService.State == DictationState.Transcribing
        });
    }

    /// <summary>
    /// Toggles dictation state (idle -> recording, recording -> transcribing).
    /// </summary>
    public async Task ToggleRecording()
    {
        if (_dictationService.State == DictationState.Idle)
        {
            try { await _dictationService.StartDictationAsync(); }
            catch (Exception ex) { _logger.LogError(ex, "StartDictation failed"); }
        }
        else if (_dictationService.State == DictationState.Recording)
        {
            try { await _dictationService.StopDictationAsync(); }
            catch (Exception ex) { _logger.LogError(ex, "StopDictation failed"); }
        }
    }

    /// <summary>
    /// Cancels ongoing transcription.
    /// </summary>
    public Task CancelTranscription()
    {
        _logger.LogInformation("CancelTranscription called from client {ConnectionId}", Context.ConnectionId);
        _dictationService.CancelTranscription();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends Enter key press to active window.
    /// </summary>
    public async Task PressEnter()
    {
        _logger.LogInformation("PressEnter called from client {ConnectionId}", Context.ConnectionId);
        try { await _keyboardSimulation.SendKeyAsync("enter"); }
        catch (Exception ex) { _logger.LogError(ex, "PressEnter failed"); }
    }

    /// <summary>
    /// Sends Ctrl+U key press to clear current line in terminal.
    /// </summary>
    public async Task ClearText()
    {
        _logger.LogInformation("ClearText called from client {ConnectionId}", Context.ConnectionId);
        try { await _keyboardSimulation.SendKeyAsync("ctrl+u"); }
        catch (Exception ex) { _logger.LogError(ex, "ClearText failed"); }
    }

    private static readonly HashSet<int> AllowedWorkspaces = [1, 4, 5, 6];

    /// <summary>
    /// Gets current workspace info (1-indexed workspace number and total count).
    /// </summary>
    public async Task<WorkspaceInfo> GetWorkspaceInfo()
    {
        var context = await _desktopContext.GetCurrentContextAsync();
        return new WorkspaceInfo
        {
            CurrentWorkspace = context.CurrentWorkspace + 1,
            TotalWorkspaces = context.TotalWorkspaces
        };
    }

    /// <summary>
    /// Switches to the specified workspace by simulating Super+N key press.
    /// </summary>
    public async Task<bool> SwitchWorkspace(int workspaceNumber)
    {
        if (!AllowedWorkspaces.Contains(workspaceNumber))
        {
            _logger.LogWarning("SwitchWorkspace rejected: workspace {Number} is not allowed", workspaceNumber);
            return false;
        }

        var context = await _desktopContext.GetCurrentContextAsync();
        if (workspaceNumber > context.TotalWorkspaces)
        {
            _logger.LogWarning("SwitchWorkspace rejected: workspace {Number} exceeds total {Total}",
                workspaceNumber, context.TotalWorkspaces);
            return false;
        }

        try
        {
            _logger.LogInformation("SwitchWorkspace {Number} from client {ConnectionId}", workspaceNumber, Context.ConnectionId);
            await _keyboardSimulation.SendKeyAsync($"super+{workspaceNumber}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SwitchWorkspace {Number} failed", workspaceNumber);
            return false;
        }
    }

    private static readonly Dictionary<string, string> AppDesktopFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["discord"] = "com.discordapp.Discord",
        ["ferdium"] = "org.ferdium.Ferdium"
    };

    private const string MoveWindowRightScript = "/home/jirka/.local/bin/move-window-right.sh";

    /// <summary>
    /// Activates a desktop application window by WM class name and snaps it to the right half.
    /// First tries D-Bus window activation, falls back to gtk-launch for tray-only apps.
    /// </summary>
    public async Task<bool> ActivateApp(string wmClass)
    {
        wmClass = wmClass?.Trim() ?? "";
        if (wmClass.Length == 0 || !AllowedApps.Contains(wmClass))
        {
            _logger.LogWarning("ActivateApp rejected: '{WmClass}' is not in allowlist", wmClass);
            return false;
        }

        try
        {
            _logger.LogInformation("ActivateApp '{WmClass}' from client {ConnectionId}", wmClass, Context.ConnectionId);

            // Try D-Bus window activation first (for already-visible windows)
            var windows = await _windowQuery.GetWindowsAsync();
            var window = windows.FirstOrDefault(w =>
                string.Equals(w.WmClass, wmClass, StringComparison.OrdinalIgnoreCase));

            if (window != null)
            {
                await _windowAction.ActivateWindowAsync(window.Id);
                _logger.LogInformation("Activated existing window '{Title}' (ID: {Id})", window.Title, window.Id);
                await SnapFocusedWindowRightAsync();
                return true;
            }

            // Window not found — app is likely minimized to tray, use gtk-launch
            if (AppDesktopFiles.TryGetValue(wmClass, out var desktopFile))
            {
                _logger.LogInformation("No window found, launching via gtk-launch {DesktopFile}", desktopFile);
                using var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "gtk-launch",
                        Arguments = desktopFile,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();

                // Wait for window to appear, then snap right
                await Task.Delay(1500);
                await SnapFocusedWindowRightAsync();
                return true;
            }

            _logger.LogWarning("No window and no desktop file for '{WmClass}'", wmClass);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ActivateApp '{WmClass}' failed", wmClass);
            return false;
        }
    }

    private async Task SnapFocusedWindowRightAsync()
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = MoveWindowRightScript,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            await process.WaitForExitAsync();
            _logger.LogInformation("Snapped focused window to right half");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to snap window to right half");
        }
    }
}

/// <summary>
/// Response model for status queries.
/// </summary>
public class StatusResponse
{
    public bool IsRecording { get; set; }
    public bool IsTranscribing { get; set; }
}

/// <summary>
/// Event types for dictation notifications.
/// </summary>
public enum DictationEventType
{
    RecordingStarted = 0,
    RecordingStopped = 1,
    TranscriptionStarted = 2,
    TranscriptionCompleted = 3
}

/// <summary>
/// Event model sent to SignalR clients.
/// </summary>
public class DictationEvent
{
    public DictationEventType EventType { get; set; }
    public string? Text { get; set; }
}

/// <summary>
/// Response model for workspace info queries.
/// </summary>
public class WorkspaceInfo
{
    public int CurrentWorkspace { get; set; }
    public int TotalWorkspaces { get; set; }
}
