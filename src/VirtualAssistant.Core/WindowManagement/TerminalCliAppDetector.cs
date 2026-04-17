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

    // Last successful detection + its timestamp. When a later probe fails transiently
    // (gdbus slow / returns empty), we serve this value instead of falling through to
    // the wrong paste shortcut. Claude Code intercepts Ctrl+V and Ctrl+Shift+V as
    // "paste image", so a Ctrl+V fallback triggers the "No image found in clipboard"
    // toast — the very bug this cache is meant to prevent. See issue #958.
    private readonly object _cacheLock = new();
    private CliAppDetectionResult? _cachedResult;
    private DateTime _cachedAtUtc = DateTime.MinValue;

    // Staleness cap: long enough to ride out a gdbus hiccup between consecutive taps
    // of "Vložit rychle", short enough that a real app switch masked by a persistent
    // gdbus outage flips us back to "no cached app" quickly.
    internal static readonly TimeSpan CacheStaleness = TimeSpan.FromSeconds(10);

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
    /// Title-based fallback for when process-tree detection fails (e.g. claude
    /// runs inside tmux, whose server is a systemd daemon and not a descendant
    /// of the focused terminal). Matches on substrings in the terminal window
    /// title that the TUI agent sets itself.
    /// </summary>
    private static readonly (string TitleMarker, string AppName, string PromptFileName)[] TitleMarkers =
    {
        ("Claude Code", "Claude Code", "ClaudeCodeCorrection"),
        ("OpenCode", "OpenCode", "OpenCodeCorrection"),
        ("OC |", "OpenCode", "OpenCodeCorrection"),
        ("Gemini", "Gemini CLI", "GeminiCorrection")
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
                // gdbus probe failed (or GNOME Shell slow / extension stalled). If the
                // last successful detection is fresh enough, serve it — otherwise we'd
                // fall back to Ctrl+V which Claude Code hijacks as "paste image".
                return TryServeCachedResult("gdbus probe returned no focused window info");
            }

            // Check if focused window is a terminal
            if (!TerminalClasses.Contains(focusedWindow.Value.WmClass))
            {
                _logger.LogDebug("Focused window is not a terminal: {WmClass}", focusedWindow.Value.WmClass);
                // Confirmed non-terminal focus: invalidate cache so a later gdbus
                // hiccup can't serve the stale CLI-app result (which would push
                // Shift+Insert into a GUI app whose "real" paste shortcut is Ctrl+V).
                ClearCache();
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
                    return CacheAndReturn(new CliAppDetectionResult(appName, promptFileName));
                }
            }

            // Fallback: when the CLI app runs under tmux, the tmux server is a
            // systemd daemon and its panes (including claude) are NOT in the
            // process tree of the focused terminal. In that case, trust the
            // terminal window title — TUI agents set it themselves (e.g.
            // "Claude Code", "OC | ...").
            var title = focusedWindow.Value.Title ?? "";
            foreach (var (titleMarker, appName, promptFileName) in TitleMarkers)
            {
                if (title.Contains(titleMarker, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation(
                        "Detected CLI app by terminal title: {AppName} (title: '{Title}')",
                        appName, title);
                    return CacheAndReturn(new CliAppDetectionResult(appName, promptFileName));
                }
            }

            _logger.LogDebug("No known CLI apps detected in terminal descendants or title");
            // Confirmed terminal without any known CLI app (e.g. plain bash): same
            // invalidation reason as the non-terminal branch.
            ClearCache();
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during CLI app detection");
            // Same fall-through as a null gdbus result: a recent cached value is
            // vastly better than a Ctrl+V fallback that hits the paste-image hijack.
            return TryServeCachedResult("unhandled exception during detection");
        }
    }

    private CliAppDetectionResult CacheAndReturn(CliAppDetectionResult result)
    {
        lock (_cacheLock)
        {
            _cachedResult = result;
            _cachedAtUtc = DateTime.UtcNow;
        }
        return result;
    }

    private void ClearCache()
    {
        lock (_cacheLock)
        {
            _cachedResult = null;
            _cachedAtUtc = DateTime.MinValue;
        }
    }

    private CliAppDetectionResult? TryServeCachedResult(string failureReason)
    {
        CliAppDetectionResult? cached;
        DateTime cachedAt;
        lock (_cacheLock)
        {
            cached = _cachedResult;
            cachedAt = _cachedAtUtc;
        }

        if (cached is not null && IsCacheFresh(cachedAt, DateTime.UtcNow))
        {
            var age = DateTime.UtcNow - cachedAt;
            _logger.LogInformation(
                "Serving cached CLI app detection {AppName} (age {AgeSeconds:F1}s, reason: {Reason})",
                cached.AppName, age.TotalSeconds, failureReason);
            return cached;
        }

        _logger.LogDebug("No usable cache (reason: {Reason})", failureReason);
        return null;
    }

    internal static bool IsCacheFresh(DateTime cachedAtUtc, DateTime nowUtc)
        => cachedAtUtc != DateTime.MinValue && nowUtc - cachedAtUtc <= CacheStaleness;

    /// <summary>
    /// Gets information about the currently focused window using GNOME window-calls extension.
    /// </summary>
    private async Task<(string WmClass, int Pid, string Title)?> GetFocusedWindowInfoAsync(CancellationToken cancellationToken)
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
            var rawJsonArray = GdbusJsonHelper.TryExtractJsonArray(output);
            if (rawJsonArray is null)
            {
                _logger.LogDebug("Could not parse D-Bus output");
                return null;
            }

            var jsonArray = GdbusJsonHelper.UnescapeQuotes(rawJsonArray);

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
                    var title = window.TryGetProperty("title", out var titleProp)
                        ? titleProp.GetString() ?? ""
                        : "";

                    if (pid > 0)
                    {
                        _logger.LogDebug("Focused window: {WmClass} \"{Title}\" (PID: {Pid})", wmClass, title, pid);
                        return (wmClass, pid, title);
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
