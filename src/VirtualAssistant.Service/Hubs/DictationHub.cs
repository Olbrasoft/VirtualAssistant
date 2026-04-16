using Microsoft.AspNetCore.SignalR;
using Olbrasoft.LinuxDesktop.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Keyboard;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;
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
    private readonly ICliAppDetector _cliAppDetector;
    private readonly ITerminalDetector _terminalDetector;
    private readonly ILogger<DictationHub> _logger;

    public DictationHub(
        IDictationService dictationService,
        IKeyboardSimulationService keyboardSimulation,
        IWindowQueryService windowQuery,
        IWindowActionService windowAction,
        IDesktopContextService desktopContext,
        ICliAppDetector cliAppDetector,
        ITerminalDetector terminalDetector,
        ILogger<DictationHub> logger)
    {
        _dictationService = dictationService;
        _keyboardSimulation = keyboardSimulation;
        _windowQuery = windowQuery;
        _windowAction = windowAction;
        _desktopContext = desktopContext;
        _cliAppDetector = cliAppDetector;
        _terminalDetector = terminalDetector;
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
    /// Toggles quick dictation (idle -> recording, recording -> raw STT + auto-paste + auto-Enter).
    /// </summary>
    public async Task ToggleQuickRecording()
    {
        if (_dictationService.State == DictationState.Idle)
        {
            try { await _dictationService.StartQuickDictationAsync(); }
            catch (Exception ex) { _logger.LogError(ex, "StartQuickDictation failed"); }
        }
        else if (_dictationService.State == DictationState.Recording)
        {
            try { await _dictationService.StopDictationAsync(); }
            catch (Exception ex) { _logger.LogError(ex, "StopDictation (quick) failed"); }
        }
    }

    /// <summary>
    /// Starts dictation in normal (LLM-corrected) mode. Used by the Remote
    /// Control unified dictation button — start with this, then call
    /// StopDictationWithMode(quick) when the user releases the button.
    /// </summary>
    public async Task StartDictation()
    {
        if (_dictationService.State == DictationState.Idle)
        {
            try { await _dictationService.StartDictationAsync(); }
            catch (Exception ex) { _logger.LogError(ex, "StartDictation failed"); }
        }
    }

    /// <summary>
    /// Stops dictation, overriding the mode that was chosen at start time.
    /// Used by the Remote Control unified dictation button: the client
    /// passes <c>quick=true</c> if the user released on the fast zone, or
    /// <c>quick=false</c> for the LLM-corrected pipeline.
    /// </summary>
    public async Task StopDictationWithMode(bool quick)
    {
        if (_dictationService.State == DictationState.Recording)
        {
            try { await _dictationService.StopDictationAsync(quick); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StopDictationWithMode(quick={Quick}) failed", quick);
            }
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
    /// Pastes from the system clipboard using the correct shortcut for the active window.
    /// Delegates to IKeyboardSimulationService.PasteFromClipboardAsync which centrally
    /// handles terminal (Ctrl+Shift+V) vs GUI (Ctrl+V) detection.
    /// </summary>
    public async Task PasteFromClipboard()
    {
        _logger.LogInformation("PasteFromClipboard called from client {ConnectionId}", Context.ConnectionId);
        try { await _keyboardSimulation.PasteFromClipboardAsync(); }
        catch (Exception ex) { _logger.LogError(ex, "PasteFromClipboard failed"); }
    }

    private static readonly string ScreenshotDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Obrázky", "Snímky obrazovky");

    private const string InsertScreenshotScript = "/home/jirka/.local/bin/insert-screenshot-path";

    /// <summary>
    /// Checks if a recent screenshot (&lt;5 min) exists in the screenshot directory.
    /// </summary>
    public Task<bool> IsScreenshotAvailable()
    {
        try
        {
            if (!Directory.Exists(ScreenshotDir)) return Task.FromResult(false);

            var cutoff = DateTime.Now.AddMinutes(-5);
            var hasRecent = Directory.EnumerateFiles(ScreenshotDir, "*.png")
                .Select(f => new FileInfo(f))
                .Any(fi => fi.LastWriteTime > cutoff);

            return Task.FromResult(hasRecent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "IsScreenshotAvailable check failed");
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Runs the insert-screenshot-path script which finds the most recent screenshot,
    /// puts its path in the clipboard, and simulates Ctrl+Shift+V to paste into terminal.
    /// </summary>
    public async Task InsertScreenshotPath()
    {
        _logger.LogInformation("InsertScreenshotPath called from client {ConnectionId}", Context.ConnectionId);
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = InsertScreenshotScript,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            await process.WaitForExitAsync();
            _logger.LogInformation("InsertScreenshotPath completed with exit code {ExitCode}", process.ExitCode);
        }
        catch (Exception ex) { _logger.LogError(ex, "InsertScreenshotPath failed"); }
    }

    /// <summary>
    /// Pastes the given text at the current cursor position using clipboard + paste simulation.
    /// </summary>
    public async Task<bool> PasteTranscription(string text)
    {
        const int maxPasteLength = 10000;

        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("PasteTranscription: empty text, ignoring");
            return false;
        }

        if (text.Length > maxPasteLength)
        {
            _logger.LogWarning("PasteTranscription rejected: text length {Length} exceeds max {Max}", text.Length, maxPasteLength);
            return false;
        }

        _logger.LogInformation("PasteTranscription from client {ConnectionId}: {Length} chars", Context.ConnectionId, text.Length);
        try
        {
            await _keyboardSimulation.TypeIntoActiveWindowAsync(text);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PasteTranscription failed");
            return false;
        }
    }

    /// <summary>
    /// Context-aware text clearing: CLI apps get End×10 + Ctrl+U×10,
    /// GUI apps get Ctrl+A + Delete, regular terminal gets Ctrl+U.
    /// </summary>
    public async Task ClearText()
    {
        _logger.LogInformation("ClearText called from client {ConnectionId}", Context.ConnectionId);
        try
        {
            var cliApp = await _cliAppDetector.DetectCliAppAsync();
            if (cliApp != null)
            {
                _logger.LogInformation("ClearText: CLI app '{App}' detected, sending End×10 + Ctrl+U×10", cliApp.AppName);
                var keys = new List<string>();
                for (var i = 0; i < 10; i++) keys.Add("end");
                for (var i = 0; i < 10; i++) keys.Add("ctrl+u");
                await _keyboardSimulation.SendKeySequenceAsync(keys);
                return;
            }

            var isTerminal = await _terminalDetector.IsTerminalActiveAsync();
            if (isTerminal)
            {
                _logger.LogInformation("ClearText: regular terminal, sending Ctrl+U");
                await _keyboardSimulation.SendKeyAsync("ctrl+u");
                return;
            }

            _logger.LogInformation("ClearText: GUI app, sending Ctrl+A + Delete");
            await _keyboardSimulation.SendKeySequenceAsync(["ctrl+a", "delete"]);
        }
        catch (Exception ex) { _logger.LogError(ex, "ClearText failed"); }
    }

    private static readonly HashSet<int> AllowedWorkspaces = [1, 2, 3, 4, 5, 6, 7, 8];

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
    /// Switches to the specified workspace by simulating Super+KP_N (numpad) key press.
    /// Does not return success/failure — clients should rely on WorkspaceChanged SignalR event.
    /// </summary>
    public async Task SwitchWorkspace(int workspaceNumber)
    {
        if (!AllowedWorkspaces.Contains(workspaceNumber))
        {
            _logger.LogWarning("SwitchWorkspace rejected: workspace {Number} is not allowed", workspaceNumber);
            return;
        }

        var context = await _desktopContext.GetCurrentContextAsync();
        if (workspaceNumber > context.TotalWorkspaces)
        {
            _logger.LogWarning("SwitchWorkspace rejected: workspace {Number} exceeds total {Total}",
                workspaceNumber, context.TotalWorkspaces);
            return;
        }

        _logger.LogInformation("SwitchWorkspace {Number} from client {ConnectionId}", workspaceNumber, Context.ConnectionId);
        try { await _keyboardSimulation.SendKeyAsync($"super+kp{workspaceNumber}"); }
        catch (Exception ex) { _logger.LogError(ex, "SwitchWorkspace {Number} failed", workspaceNumber); }
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
    TranscriptionCompleted = 3,
    RawTranscriptionCompleted = 4,
    QuickTranscriptionCompleted = 5
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
