using System.Diagnostics;
using Olbrasoft.LinuxDesktop.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Keyboard;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Hubs;
using IDesktopContextService = Olbrasoft.VirtualAssistant.Core.Services.IDesktopContextService;

namespace Olbrasoft.VirtualAssistant.Service.Hubs.Services;

/// <inheritdoc />
public class RemoteDesktopCommands : IRemoteDesktopCommands
{
    private static readonly HashSet<string> AllowedApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "discord", "ferdium"
    };

    private static readonly Dictionary<string, string> AppDesktopFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["discord"] = "com.discordapp.Discord",
        ["ferdium"] = "org.ferdium.Ferdium"
    };

    private static readonly HashSet<int> AllowedWorkspaces = [1, 2, 3, 4, 5, 6, 7, 8];

    private const string MoveWindowRightScript = "/home/jirka/.local/bin/move-window-right.sh";

    private readonly ILogger<RemoteDesktopCommands> _logger;
    private readonly IDesktopContextService _desktopContext;
    private readonly IWindowQueryService _windowQuery;
    private readonly IWindowActionService _windowAction;
    private readonly IKeyboardSimulationService _keyboardSimulation;

    public RemoteDesktopCommands(
        ILogger<RemoteDesktopCommands> logger,
        IDesktopContextService desktopContext,
        IWindowQueryService windowQuery,
        IWindowActionService windowAction,
        IKeyboardSimulationService keyboardSimulation)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _desktopContext = desktopContext ?? throw new ArgumentNullException(nameof(desktopContext));
        _windowQuery = windowQuery ?? throw new ArgumentNullException(nameof(windowQuery));
        _windowAction = windowAction ?? throw new ArgumentNullException(nameof(windowAction));
        _keyboardSimulation = keyboardSimulation ?? throw new ArgumentNullException(nameof(keyboardSimulation));
    }

    public async Task<string> GetFocusedAppAsync()
    {
        var context = await _desktopContext.GetCurrentContextAsync();
        return context.ActiveWindowClass ?? "";
    }

    public async Task<bool> CloseAppAsync(string wmClass)
    {
        wmClass = wmClass?.Trim() ?? "";
        if (wmClass.Length == 0 || !AllowedApps.Contains(wmClass))
        {
            _logger.LogWarning("CloseApp rejected: '{WmClass}' is not in allowlist", wmClass);
            return false;
        }

        try
        {
            var context = await _desktopContext.GetCurrentContextAsync();
            if (!string.Equals(context.ActiveWindowClass, wmClass, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("CloseApp '{WmClass}' rejected: focused window is '{Focused}'",
                    wmClass, context.ActiveWindowClass);
                return false;
            }

            _logger.LogInformation("CloseApp '{WmClass}'", wmClass);
            await _keyboardSimulation.SendKeyAsync("alt+F4");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CloseApp '{WmClass}' failed", wmClass);
            return false;
        }
    }

    public async Task<WorkspaceInfo> GetWorkspaceInfoAsync()
    {
        var context = await _desktopContext.GetCurrentContextAsync();
        return new WorkspaceInfo
        {
            CurrentWorkspace = context.CurrentWorkspace + 1,
            TotalWorkspaces = context.TotalWorkspaces
        };
    }

    public async Task SwitchWorkspaceAsync(int workspaceNumber)
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

        _logger.LogInformation("SwitchWorkspace {Number}", workspaceNumber);
        try { await _keyboardSimulation.SendKeyAsync($"super+kp{workspaceNumber}"); }
        catch (Exception ex) { _logger.LogError(ex, "SwitchWorkspace {Number} failed", workspaceNumber); }
    }

    public async Task<bool> ActivateAppAsync(string wmClass)
    {
        wmClass = wmClass?.Trim() ?? "";
        if (wmClass.Length == 0 || !AllowedApps.Contains(wmClass))
        {
            _logger.LogWarning("ActivateApp rejected: '{WmClass}' is not in allowlist", wmClass);
            return false;
        }

        try
        {
            _logger.LogInformation("ActivateApp '{WmClass}'", wmClass);

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

            if (AppDesktopFiles.TryGetValue(wmClass, out var desktopFile))
            {
                _logger.LogInformation("No window found, launching via gtk-launch {DesktopFile}", desktopFile);
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "gtk-launch",
                        Arguments = desktopFile,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();

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
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
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
