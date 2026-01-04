namespace VirtualAssistant.Core.Models;

/// <summary>
/// Represents current desktop context (workspace, focused window, active application).
/// </summary>
public record DesktopContext(
    int CurrentWorkspace,
    int TotalWorkspaces,
    string ActiveWindowTitle,
    string ActiveWindowClass,
    string ActiveApplication,
    DateTime Timestamp
);

/// <summary>
/// Represents a change in desktop context.
/// </summary>
public record DesktopContextChange(
    DesktopContext PreviousContext,
    DesktopContext NewContext,
    ChangeType Type
);

/// <summary>
/// Type of desktop context change.
/// </summary>
public enum ChangeType
{
    WorkspaceChanged,
    WindowFocusChanged,
    ApplicationChanged
}

/// <summary>
/// Event args for workspace change events from D-Bus signals.
/// </summary>
public record WorkspaceChangedEventArgs(
    int NewIndex,
    int TotalWorkspaces
);

/// <summary>
/// Event args for focus change events from D-Bus signals.
/// </summary>
public record FocusChangedEventArgs(
    string WindowTitle,
    string AppId,
    string WmClass
);
