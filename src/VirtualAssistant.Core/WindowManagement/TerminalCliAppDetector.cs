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
    /// A CLI agent we know how to detect, plus everything the three detection
    /// strategies need: the process name we'd find in the terminal's descendants,
    /// the substrings the TUI sets in the terminal title, and the prefix the
    /// user's tmux wrapper gives to sessions it spawns for this agent. Keeping
    /// these together means adding a new agent is a one-line change — there's
    /// no risk of the three detection paths drifting out of sync.
    /// </summary>
    private sealed record KnownAgent(
        string AppName,
        string PromptFileName,
        string ProcessName,
        string[] TitleMarkers,
        string TmuxSessionPrefix);

    private static readonly KnownAgent[] KnownAgents =
    {
        new("Claude Code", "ClaudeCodeCorrection", "claude",
            new[] { "Claude Code" }, "claude-"),
        new("OpenCode", "OpenCodeCorrection", "opencode",
            new[] { "OpenCode", "OC |" }, "opencode-"),
        new("Gemini CLI", "GeminiCorrection", "gemini",
            new[] { "Gemini" }, "gemini-")
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
            foreach (var agent in KnownAgents)
            {
                if (await IsProcessAmongPidsAsync(agent.ProcessName, descendantPids, cancellationToken))
                {
                    _logger.LogInformation("Detected CLI app in terminal: {AppName} (process: {ProcessName})",
                        agent.AppName, agent.ProcessName);
                    return CacheAndReturn(new CliAppDetectionResult(agent.AppName, agent.PromptFileName));
                }
            }

            // Fallback: when the CLI app runs under tmux, the tmux server is a
            // systemd daemon and its panes (including claude) are NOT in the
            // process tree of the focused terminal. In that case, trust the
            // terminal window title — TUI agents set it themselves (e.g.
            // "Claude Code", "OC | ...").
            var title = focusedWindow.Value.Title ?? "";
            foreach (var agent in KnownAgents)
            {
                foreach (var marker in agent.TitleMarkers)
                {
                    if (title.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation(
                            "Detected CLI app by terminal title: {AppName} (title: '{Title}')",
                            agent.AppName, title);
                        return CacheAndReturn(new CliAppDetectionResult(agent.AppName, agent.PromptFileName));
                    }
                }
            }

            // Last fallback: the user's bashrc wrapper runs `tmux new-session -s
            // claude-<repo>-<tty> -- claude "$@"`, so the tmux *client* is in the
            // terminal's descendants but the claude process lives under the tmux
            // server (systemd-scoped, not in the process tree). Ask tmux which
            // session that client is attached to and match the session name prefix.
            var cliAppFromTmux = await DetectCliAppViaTmuxAsync(descendantPids, cancellationToken);
            if (cliAppFromTmux != null)
            {
                return CacheAndReturn(cliAppFromTmux);
            }

            _logger.LogDebug("No known CLI apps detected in terminal descendants, title or tmux sessions");
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
    /// Parses the output of <c>tmux list-clients -F "#{client_pid}:#{session_name}"</c>
    /// into a map of client PID → session name. Blank and malformed lines are
    /// skipped. Session names may contain colons so we split on the first ':' only.
    /// </summary>
    internal static Dictionary<int, string> ParseTmuxClients(string? output)
    {
        var map = new Dictionary<int, string>();
        if (string.IsNullOrWhiteSpace(output))
        {
            return map;
        }

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            var separator = trimmed.IndexOf(':');
            if (separator <= 0 || separator == trimmed.Length - 1)
            {
                continue;
            }

            if (!int.TryParse(trimmed[..separator], out var pid))
            {
                continue;
            }

            var session = trimmed[(separator + 1)..];
            if (!string.IsNullOrEmpty(session))
            {
                map[pid] = session;
            }
        }

        return map;
    }

    /// <summary>
    /// Matches a tmux session name against the known-agent prefixes and returns
    /// the CLI-app detection result if the prefix is known, otherwise null.
    /// </summary>
    internal static CliAppDetectionResult? MatchTmuxSessionName(string sessionName)
    {
        foreach (var agent in KnownAgents)
        {
            if (sessionName.StartsWith(agent.TmuxSessionPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return new CliAppDetectionResult(agent.AppName, agent.PromptFileName);
            }
        }
        return null;
    }

    /// <summary>
    /// Runs <c>tmux list-clients</c> and returns the first descendant PID's
    /// session whose name matches a known CLI-agent prefix.
    /// </summary>
    private async Task<CliAppDetectionResult?> DetectCliAppViaTmuxAsync(HashSet<int> descendantPids, CancellationToken cancellationToken)
    {
        if (descendantPids.Count == 0)
        {
            return null;
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "tmux",
                    Arguments = "list-clients -F \"#{client_pid}:#{session_name}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            try
            {
                var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);

                if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                {
                    _logger.LogDebug("tmux list-clients returned no data (exit {ExitCode})", process.ExitCode);
                    return null;
                }

                // Iterate the (small) client list and probe the descendant set
                // instead of the other way round — a long-running terminal can
                // accumulate hundreds of descendants while `tmux list-clients`
                // is at most one row per attached terminal.
                var clients = ParseTmuxClients(output);
                foreach (var (clientPid, sessionName) in clients)
                {
                    if (!descendantPids.Contains(clientPid))
                    {
                        continue;
                    }

                    var match = MatchTmuxSessionName(sessionName);
                    if (match != null)
                    {
                        _logger.LogInformation(
                            "Detected CLI app via tmux session '{Session}' (client PID {Pid}): {AppName}",
                            sessionName, clientPid, match.AppName);
                        return match;
                    }
                }

                return null;
            }
            catch (OperationCanceledException)
            {
                // Caller cancelled mid-probe. Kill the tmux child so it can't
                // outlive the detection call and leak into ps output. Swallow
                // kill errors — the process may already have exited.
                try { process.Kill(entireProcessTree: true); } catch { }
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to query tmux clients for CLI app detection");
            return null;
        }
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
