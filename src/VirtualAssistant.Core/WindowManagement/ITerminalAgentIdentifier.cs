namespace Olbrasoft.VirtualAssistant.Core.WindowManagement;

/// <summary>
/// Identifies the known CLI agent (Claude Code, OpenCode, Gemini CLI) running
/// inside a given terminal window. Combines the three signals that can betray
/// an agent in a terminal:
/// <list type="number">
/// <item>window title markers (fast, but fails when tmux/terminator does not
/// propagate the TUI title to the outer X11 window — e.g. terminator titled
/// <c>/bin/bash</c> even though <c>claude</c> is running under a tmux session);</item>
/// <item>process-tree descendants of the terminal PID (catches <c>claude</c>
/// launched directly as a child of the terminal);</item>
/// <item>tmux session names attached to a descendant tmux client (catches
/// wrapper-spawned <c>claude-&lt;repo&gt;-&lt;tty&gt;</c> sessions whose server is
/// systemd-scoped and therefore outside the terminal's descendant tree).</item>
/// </list>
/// Both <c>TerminalCliAppDetector</c> (focused-window detection for the
/// notifications / civility-trim paths) and <c>DictationFocusRouter</c>
/// (per-window scan for Quick Dictation auto-focus) share this service so the
/// two code paths cannot drift: anywhere we decide whether a terminal hosts
/// Claude Code, we decide the same way.
/// </summary>
public interface ITerminalAgentIdentifier
{
    /// <summary>
    /// Returns the known agent running in the terminal whose outer window has
    /// the given <paramref name="title"/> and PID <paramref name="terminalPid"/>,
    /// or null if no known agent is detected. The caller is responsible for
    /// having already confirmed that the window is a terminal (typically via
    /// WmClass); this method makes no WmClass check of its own.
    /// </summary>
    Task<KnownAgent?> IdentifyAsync(string? title, int terminalPid, CancellationToken cancellationToken);
}
