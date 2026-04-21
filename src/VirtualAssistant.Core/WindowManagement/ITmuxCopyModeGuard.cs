namespace Olbrasoft.VirtualAssistant.Core.WindowManagement;

/// <summary>
/// Ensures no active tmux pane is lingering in copy-mode before VirtualAssistant
/// simulates a paste. In tmux copy-mode every keystroke — including the
/// <c>Shift+Insert</c> paste VA emits — is consumed by tmux's own scroll/search
/// bindings and never reaches the program running in the pane (Claude Code,
/// OpenCode, Gemini CLI, …). With <c>mouse on</c> in the user's config tmux
/// auto-enters copy-mode on any mouse-wheel scroll, so the bug appears "out of
/// nowhere" and the only visual cue is the pane cursor blinking inverted
/// (black/white) instead of solid. See #1050 in Olbrasoft/VirtualAssistant.
/// </summary>
public interface ITmuxCopyModeGuard
{
    /// <summary>
    /// Queries <c>tmux list-panes -a</c>, finds every active pane currently in
    /// copy-mode, and issues <c>send-keys -X cancel</c> to each so the pane's
    /// input pipeline is live again. Silently no-ops when tmux is not installed
    /// or reports no such pane — paste callers must not be blocked by guard
    /// failures.
    /// </summary>
    Task EnsureNotInCopyModeAsync(CancellationToken cancellationToken);
}
