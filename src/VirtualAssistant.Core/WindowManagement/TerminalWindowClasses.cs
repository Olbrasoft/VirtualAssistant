namespace Olbrasoft.VirtualAssistant.Core.WindowManagement;

/// <summary>
/// The WM_CLASS values we recognise as terminal emulators. Shared between
/// <see cref="TerminalCliAppDetector"/> (focused-window CLI detection) and
/// <see cref="ITerminalAgentIdentifier"/> callers (e.g. the dictation focus
/// router) so both paths treat the same set of windows as "terminals" and
/// we don't have to wonder whether a given emulator is handled in one path
/// and not the other.
/// </summary>
public static class TerminalWindowClasses
{
    /// <summary>
    /// Case-insensitive set of known terminal WmClass values.
    /// </summary>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "kitty", "gnome-terminal", "gnome-terminal-server", "org.gnome.Terminal",
        "konsole", "xfce4-terminal", "mate-terminal", "tilix", "terminator",
        "alacritty", "wezterm", "foot", "xterm", "urxvt", "st", "terminology",
    };

    /// <summary>
    /// Returns true if <paramref name="wmClass"/> identifies a terminal emulator
    /// we know how to inspect. Null or empty WmClass values return false.
    /// </summary>
    public static bool IsTerminal(string? wmClass) =>
        !string.IsNullOrEmpty(wmClass) && All.Contains(wmClass);
}
