namespace Olbrasoft.VirtualAssistant.Core.WindowManagement;

/// <summary>
/// Detects CLI agents running under tmux. When the user's bashrc wrapper
/// runs <c>tmux new-session -s claude-&lt;repo&gt;-&lt;tty&gt; -- claude "$@"</c>,
/// the tmux server is systemd-scoped and the claude process is not in the
/// focused terminal's descendant tree — so process-tree matching misses.
/// This matcher asks tmux directly which session a descendant tmux client
/// is attached to and matches the session name against the agent registry.
/// </summary>
public interface ITmuxCliAppMatcher
{
    /// <summary>
    /// Runs <c>tmux list-clients</c> and returns the first descendant-PID's
    /// session whose name prefix matches a known CLI-agent, or null if none
    /// match or tmux is unavailable.
    /// </summary>
    Task<CliAppDetectionResult?> MatchAsync(IReadOnlySet<int> descendantPids, CancellationToken cancellationToken);
}
