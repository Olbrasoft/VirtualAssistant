using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.WindowManagement;

/// <inheritdoc />
public sealed class TerminalAgentIdentifier : ITerminalAgentIdentifier
{
    private readonly ILogger<TerminalAgentIdentifier> _logger;
    private readonly ITmuxCliAppMatcher _tmuxMatcher;

    public TerminalAgentIdentifier(
        ILogger<TerminalAgentIdentifier> logger,
        ITmuxCliAppMatcher tmuxMatcher)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tmuxMatcher = tmuxMatcher ?? throw new ArgumentNullException(nameof(tmuxMatcher));
    }

    /// <inheritdoc />
    public async Task<KnownAgent?> IdentifyAsync(string? title, int terminalPid, CancellationToken cancellationToken)
    {
        // Order: process-tree → title → tmux session. Process-tree is the
        // strongest signal (the actual process is literally running under us),
        // title is cheapest when it works, tmux is the fallback for
        // systemd-scoped wrapper sessions.
        var descendantPids = await GetDescendantProcessesAsync(terminalPid, cancellationToken);

        foreach (var agent in CliAgentRegistry.KnownAgents)
        {
            if (await IsProcessAmongPidsAsync(agent.ProcessName, descendantPids, cancellationToken))
            {
                _logger.LogInformation(
                    "Detected CLI app in terminal (pid {Pid}): {AppName} (process: {ProcessName})",
                    terminalPid, agent.AppName, agent.ProcessName);
                return agent;
            }
        }

        if (CliAgentRegistry.FindByTitle(title) is { } titleMatch)
        {
            _logger.LogInformation(
                "Detected CLI app by terminal title: {AppName} (title: '{Title}')",
                titleMatch.AppName, title);
            return titleMatch;
        }

        if (await _tmuxMatcher.MatchAsync(descendantPids, cancellationToken) is { } tmuxMatch)
        {
            // TmuxCliAppMatcher already logs the session name — we only need
            // to map its CliAppDetectionResult back to the KnownAgent record
            // so callers get the same type regardless of detection path.
            return CliAgentRegistry.KnownAgents.FirstOrDefault(a =>
                string.Equals(a.AppName, tmuxMatch.AppName, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    /// <summary>
    /// BFS walk of the process tree rooted at <paramref name="parentPid"/>.
    /// Each level spawns one <c>pgrep -P</c>, so cost scales with depth, not
    /// breadth — a terminator running a shell running tmux running claude is
    /// typically ~4 levels.
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
            var output = await RunPgrepAsync($"-P {parentPid}", cancellationToken);
            if (output is null) return children;

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(line.Trim(), out var pid))
                    children.Add(pid);
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
            var output = await RunPgrepAsync($"-x {processName}", cancellationToken);
            if (output is null) return false;

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(line.Trim(), out var pid) && pids.Contains(pid))
                {
                    _logger.LogDebug("Found {ProcessName} (PID: {Pid}) among terminal descendants", processName, pid);
                    return true;
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

    // Spawns pgrep with the given arguments; returns stdout on success or null
    // if pgrep exits non-zero (no match is exit 1, which is legitimate). Kills
    // the child on caller cancellation before rethrowing so hotkey spam cannot
    // accumulate short-lived pgrep processes. Mirrors the safeguard added in
    // PR #1003 for the original in-detector helper.
    private static async Task<string?> RunPgrepAsync(string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "pgrep",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();
        string output;
        try
        {
            output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) ? output : null;
    }
}
