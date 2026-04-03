using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.WindowManagement;

/// <summary>
/// Detects CLI applications running in terminal emulators by checking child processes
/// of the active terminal window. This is useful when CLI apps (like Claude Code)
/// change the terminal window title.
/// </summary>
public class TerminalCliAppDetector : ICliAppDetector
{
    private readonly ILogger<TerminalCliAppDetector> _logger;

    /// <summary>
    /// Known CLI applications to detect, mapped to their prompt configurations.
    /// Key: process name pattern, Value: (AppName, PromptFileName)
    /// </summary>
    private static readonly Dictionary<string, (string AppName, string PromptFileName)> KnownCliApps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude"] = ("Claude Code", "ClaudeCodeCorrection"),
        ["opencode"] = ("OpenCode", "OpenCodeCorrection"),
        ["gemini"] = ("Gemini CLI", "GeminiCorrection")
    };

    /// <summary>
    /// Terminal window classes that we know how to detect CLI apps in.
    /// </summary>
    private static readonly HashSet<string> TerminalClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "kitty", "gnome-terminal", "gnome-terminal-server", "org.gnome.Terminal",
        "konsole", "xfce4-terminal", "mate-terminal", "tilix", "terminator",
        "alacritty", "wezterm", "foot", "xterm", "urxvt", "st", "terminology"
    };

    public TerminalCliAppDetector(ILogger<TerminalCliAppDetector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CliAppDetectionResult?> DetectCliAppAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Get focused window info including PID
            var focusedWindow = await GetFocusedWindowInfoAsync(cancellationToken);
            if (focusedWindow == null)
            {
                _logger.LogDebug("Could not get focused window info");
                return null;
            }

            // Check if focused window is a terminal
            if (!TerminalClasses.Contains(focusedWindow.Value.WmClass))
            {
                _logger.LogDebug("Focused window is not a terminal: {WmClass}", focusedWindow.Value.WmClass);
                return null;
            }

            _logger.LogDebug("Terminal detected: {WmClass} (PID: {Pid}), checking for CLI apps...",
                focusedWindow.Value.WmClass, focusedWindow.Value.Pid);

            // Get all descendant processes of the terminal
            var descendantPids = await GetDescendantProcessesAsync(focusedWindow.Value.Pid, cancellationToken);
            _logger.LogDebug("Found {Count} descendant processes for terminal PID {Pid}",
                descendantPids.Count, focusedWindow.Value.Pid);

            // Check if any known CLI app is among descendants
            foreach (var (processName, (appName, promptFileName)) in KnownCliApps)
            {
                if (await IsProcessAmongPidsAsync(processName, descendantPids, cancellationToken))
                {
                    _logger.LogInformation("Detected CLI app in terminal: {AppName} (process: {ProcessName})",
                        appName, processName);
                    return new CliAppDetectionResult(appName, promptFileName);
                }
            }

            _logger.LogDebug("No known CLI apps detected in terminal descendants");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during CLI app detection");
            return null;
        }
    }

    /// <summary>
    /// Gets information about the currently focused window using GNOME window-calls extension.
    /// </summary>
    private async Task<(string WmClass, int Pid)?> GetFocusedWindowInfoAsync(CancellationToken cancellationToken)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "gdbus",
                    Arguments = "call --session --dest org.gnome.Shell " +
                               "--object-path /org/gnome/Shell/Extensions/Windows " +
                               "--method org.gnome.Shell.Extensions.Windows.List",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                _logger.LogDebug("D-Bus window-calls returned no data or failed");
                return null;
            }

            // Extract JSON array from gdbus output: ('[{...}]',)
            var jsonStart = output.IndexOf('[');
            var jsonEnd = output.LastIndexOf(']');
            if (jsonStart < 0 || jsonEnd <= jsonStart)
            {
                _logger.LogDebug("Could not parse D-Bus output");
                return null;
            }

            var jsonArray = output.Substring(jsonStart, jsonEnd - jsonStart + 1);
            var windows = JsonSerializer.Deserialize<JsonElement>(jsonArray);

            foreach (var window in windows.EnumerateArray())
            {
                if (window.TryGetProperty("focus", out var focusProp) && focusProp.GetBoolean())
                {
                    var wmClass = window.TryGetProperty("wm_class", out var wmClassProp)
                        ? wmClassProp.GetString() ?? ""
                        : "";
                    var pid = window.TryGetProperty("pid", out var pidProp)
                        ? pidProp.GetInt32()
                        : 0;

                    if (pid > 0)
                    {
                        _logger.LogDebug("Focused window: {WmClass}, PID: {Pid}", wmClass, pid);
                        return (wmClass, pid);
                    }
                }
            }

            _logger.LogDebug("No focused window found");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Failed to parse D-Bus JSON response");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get focused window info");
            return null;
        }
    }

    /// <summary>
    /// Gets all descendant process IDs (children, grandchildren, etc.) of a given PID.
    /// </summary>
    private async Task<HashSet<int>> GetDescendantProcessesAsync(int parentPid, CancellationToken cancellationToken)
    {
        var descendants = new HashSet<int>();
        var toProcess = new Queue<int>();
        toProcess.Enqueue(parentPid);

        while (toProcess.Count > 0)
        {
            var currentPid = toProcess.Dequeue();
            var children = await GetChildPidsAsync(currentPid, cancellationToken);

            foreach (var childPid in children)
            {
                if (descendants.Add(childPid))
                {
                    toProcess.Enqueue(childPid);
                }
            }
        }

        return descendants;
    }

    /// <summary>
    /// Gets immediate child PIDs of a process.
    /// </summary>
    private async Task<List<int>> GetChildPidsAsync(int parentPid, CancellationToken cancellationToken)
    {
        var children = new List<int>();

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pgrep",
                    Arguments = $"-P {parentPid}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(line.Trim(), out var pid))
                    {
                        children.Add(pid);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get child PIDs for {ParentPid}", parentPid);
        }

        return children;
    }

    /// <summary>
    /// Checks if a process with the given name is running with any of the specified PIDs.
    /// </summary>
    private async Task<bool> IsProcessAmongPidsAsync(string processName, HashSet<int> pids, CancellationToken cancellationToken)
    {
        if (pids.Count == 0)
            return false;

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pgrep",
                    Arguments = $"-x {processName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(line.Trim(), out var pid) && pids.Contains(pid))
                    {
                        _logger.LogDebug("Found {ProcessName} (PID: {Pid}) among terminal descendants", processName, pid);
                        return true;
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to check if process '{ProcessName}' is among PIDs", processName);
            return false;
        }
    }
}
