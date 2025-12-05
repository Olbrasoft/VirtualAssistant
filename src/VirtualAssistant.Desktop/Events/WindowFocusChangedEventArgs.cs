using VirtualAssistant.Desktop.Models;

namespace VirtualAssistant.Desktop.Events;

/// <summary>
/// Event arguments for window focus change events.
/// </summary>
public class WindowFocusChangedEventArgs : EventArgs
{
    /// <summary>
    /// The previously focused window, or null if this is the first focus event.
    /// </summary>
    public WindowInfo? PreviousWindow { get; }

    /// <summary>
    /// The currently focused window.
    /// </summary>
    public WindowInfo CurrentWindow { get; }

    public WindowFocusChangedEventArgs(WindowInfo? previousWindow, WindowInfo currentWindow)
    {
        PreviousWindow = previousWindow;
        CurrentWindow = currentWindow;
    }
}
