using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.WindowManagement;

/// <inheritdoc />
public sealed class TmuxCliAppMatcher : ITmuxCliAppMatcher
{
    private readonly ILogger<TmuxCliAppMatcher> _logger;

    public TmuxCliAppMatcher(ILogger<TmuxCliAppMatcher> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CliAppDetectionResult?> MatchAsync(IReadOnlySet<int> descendantPids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descendantPids);
        if (descendantPids.Count == 0) return null;

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
                    CreateNoWindow = true,
                },
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
                    if (!descendantPids.Contains(clientPid)) continue;

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
                // outlive the detection call and leak into ps output.
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
    /// Parses the output of <c>tmux list-clients -F "#{client_pid}:#{session_name}"</c>
    /// into a map of client PID → session name. Blank and malformed lines are
    /// skipped. Session names may contain colons so we split on the first ':' only.
    /// </summary>
    public static Dictionary<int, string> ParseTmuxClients(string? output)
    {
        var map = new Dictionary<int, string>();
        if (string.IsNullOrWhiteSpace(output)) return map;

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            var separator = trimmed.IndexOf(':');
            if (separator <= 0 || separator == trimmed.Length - 1) continue;

            if (!int.TryParse(trimmed[..separator], out var pid)) continue;

            var session = trimmed[(separator + 1)..];
            if (!string.IsNullOrEmpty(session)) map[pid] = session;
        }

        return map;
    }

    /// <summary>
    /// Matches a tmux session name against the known-agent prefixes and returns
    /// the CLI-app detection result if the prefix is known, otherwise null.
    /// </summary>
    public static CliAppDetectionResult? MatchTmuxSessionName(string sessionName)
    {
        var agent = CliAgentRegistry.FindByTmuxSession(sessionName);
        return agent is null ? null : new CliAppDetectionResult(agent.AppName, agent.PromptFileName);
    }
}
