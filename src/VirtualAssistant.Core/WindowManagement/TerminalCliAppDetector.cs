using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.WindowManagement;

/// <summary>
/// Detects CLI applications (Claude Code, OpenCode, Gemini CLI) running inside
/// the currently focused terminal. The class now delegates the three detection
/// strategies to focused collaborators:
/// <list type="bullet">
/// <item><see cref="IGdbusWindowDetector"/> — resolves the focused window via GNOME Shell.</item>
/// <item><see cref="CliAppDetectionCache"/> — serves the last successful result during transient gdbus failures.</item>
/// <item><see cref="ITmuxCliAppMatcher"/> — matches tmux session names when the CLI app is outside the focused terminal's descendant tree.</item>
/// </list>
/// Process-tree + title-marker matching live inline — both are small and share
/// the <see cref="CliAgentRegistry"/> shortlist used by the tmux matcher.
/// </summary>
public class TerminalCliAppDetector : ICliAppDetector
{
    /// <summary>
    /// Terminal window classes that we know how to detect CLI apps in.
    /// </summary>
    private static readonly HashSet<string> TerminalClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "kitty", "gnome-terminal", "gnome-terminal-server", "org.gnome.Terminal",
        "konsole", "xfce4-terminal", "mate-terminal", "tilix", "terminator",
        "alacritty", "wezterm", "foot", "xterm", "urxvt", "st", "terminology",
    };

    private readonly ILogger<TerminalCliAppDetector> _logger;
    private readonly IGdbusWindowDetector _gdbus;
    private readonly ITmuxCliAppMatcher _tmuxMatcher;
    private readonly CliAppDetectionCache _cache;

    public TerminalCliAppDetector(
        ILogger<TerminalCliAppDetector> logger,
        IGdbusWindowDetector gdbus,
        ITmuxCliAppMatcher tmuxMatcher,
        CliAppDetectionCache cache)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _gdbus = gdbus ?? throw new ArgumentNullException(nameof(gdbus));
        _tmuxMatcher = tmuxMatcher ?? throw new ArgumentNullException(nameof(tmuxMatcher));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<CliAppDetectionResult?> DetectCliAppAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var focusedWindow = await _gdbus.GetFocusedWindowInfoAsync(cancellationToken);
            if (focusedWindow == null)
            {
                return _cache.TryGet("gdbus probe returned no focused window info");
            }

            if (!TerminalClasses.Contains(focusedWindow.Value.WmClass))
            {
                _logger.LogDebug("Focused window is not a terminal: {WmClass}", focusedWindow.Value.WmClass);
                // Confirmed non-terminal focus: invalidate cache so a later gdbus
                // hiccup can't serve the stale CLI-app result.
                _cache.Clear();
                return null;
            }

            _logger.LogDebug("Terminal detected: {WmClass} (PID: {Pid}), checking for CLI apps...",
                focusedWindow.Value.WmClass, focusedWindow.Value.Pid);

            var descendantPids = await GetDescendantProcessesAsync(focusedWindow.Value.Pid, cancellationToken);
            _logger.LogDebug("Found {Count} descendant processes for terminal PID {Pid}",
                descendantPids.Count, focusedWindow.Value.Pid);

            if (await MatchByProcessTreeAsync(descendantPids, cancellationToken) is { } processMatch)
                return CacheAndReturn(processMatch);

            if (MatchByTitle(focusedWindow.Value.Title) is { } titleMatch)
                return CacheAndReturn(titleMatch);

            if (await _tmuxMatcher.MatchAsync(descendantPids, cancellationToken) is { } tmuxMatch)
                return CacheAndReturn(tmuxMatch);

            _logger.LogDebug("No known CLI apps detected in terminal descendants, title or tmux sessions");
            // Confirmed terminal without any known CLI app (e.g. plain bash).
            _cache.Clear();
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during CLI app detection");
            return _cache.TryGet("unhandled exception during detection");
        }
    }

    private CliAppDetectionResult CacheAndReturn(CliAppDetectionResult result)
    {
        _cache.Set(result);
        return result;
    }

    private async Task<CliAppDetectionResult?> MatchByProcessTreeAsync(HashSet<int> descendantPids, CancellationToken cancellationToken)
    {
        foreach (var agent in CliAgentRegistry.KnownAgents)
        {
            if (await IsProcessAmongPidsAsync(agent.ProcessName, descendantPids, cancellationToken))
            {
                _logger.LogInformation("Detected CLI app in terminal: {AppName} (process: {ProcessName})",
                    agent.AppName, agent.ProcessName);
                return new CliAppDetectionResult(agent.AppName, agent.PromptFileName);
            }
        }
        return null;
    }

    private CliAppDetectionResult? MatchByTitle(string? title)
    {
        var agent = CliAgentRegistry.FindByTitle(title);
        if (agent is null) return null;

        _logger.LogInformation(
            "Detected CLI app by terminal title: {AppName} (title: '{Title}')",
            agent.AppName, title);
        return new CliAppDetectionResult(agent.AppName, agent.PromptFileName);
    }

    /// <summary>
    /// Walks the process tree rooted at <paramref name="parentPid"/> and returns
    /// the full descendant PID set. BFS — each iteration spawns one pgrep, so
    /// depth scales with tree depth, not breadth.
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
                    toProcess.Enqueue(childPid);
            }
        }

        return descendants;
    }

    private async Task<List<int>> GetChildPidsAsync(int parentPid, CancellationToken cancellationToken)
    {
        var children = new List<int>();

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pgrep",
                    Arguments = $"-P {parentPid}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(line.Trim(), out var pid))
                        children.Add(pid);
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get child PIDs for {ParentPid}", parentPid);
        }

        return children;
    }

    private async Task<bool> IsProcessAmongPidsAsync(string processName, HashSet<int> pids, CancellationToken cancellationToken)
    {
        if (pids.Count == 0) return false;

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pgrep",
                    Arguments = $"-x {processName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
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
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to check if process '{ProcessName}' is among PIDs", processName);
            return false;
        }
    }
}
