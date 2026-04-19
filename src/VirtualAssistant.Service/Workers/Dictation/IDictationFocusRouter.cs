namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <summary>
/// Quick-Dictation-only: if the current workspace has exactly one Claude
/// Code window and the user's current focus is somewhere else (browser,
/// non-Claude terminal, desktop), switch focus to that Claude Code window
/// before the paste. Keeps the "user pressed fast from anywhere → text
/// lands in Claude Code" assumption alive even when the user left focus
/// on a non-editable surface (e.g. the Remote Control web UI).
/// Gnome Text Editor is an explicit exception: it's a dictation scratchpad
/// and must never be hijacked.
/// </summary>
public interface IDictationFocusRouter
{
    /// <summary>
    /// Inspects the current workspace and, if the conditions above hold,
    /// activates the single Claude Code window and waits briefly for
    /// GNOME to settle focus so the subsequent xdotool paste lands in
    /// the new target instead of the outgoing window. Returns true when
    /// a switch happened.
    /// </summary>
    Task<bool> TryFocusClaudeCodeIfApplicableAsync(CancellationToken cancellationToken);
}
