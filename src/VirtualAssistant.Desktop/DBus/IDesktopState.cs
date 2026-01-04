using Tmds.DBus;

namespace VirtualAssistant.Desktop.DBus;

/// <summary>
/// Event args for workspace change D-Bus signal.
/// </summary>
public readonly record struct WorkspaceChangedArgs(int NewIndex, int TotalWorkspaces);

/// <summary>
/// Event args for focus change D-Bus signal.
/// </summary>
public readonly record struct FocusChangedArgs(string WindowTitle, string AppId, string WmClass);

/// <summary>
/// D-Bus interface for org.olbrasoft.Desktop (focus-tracker GNOME extension).
/// Provides properties and signals for desktop state monitoring.
/// </summary>
[DBusInterface("org.olbrasoft.Desktop")]
internal interface IDesktopState : IDBusObject
{
    /// <summary>
    /// Gets a property value from the D-Bus service.
    /// Supported properties: CurrentWorkspace, TotalWorkspaces, ActiveWindow, ActiveApplication
    /// </summary>
    Task<T> GetAsync<T>(string propertyName);

    /// <summary>
    /// Subscribes to WorkspaceChanged D-Bus signal.
    /// Signal is emitted when user switches workspaces.
    /// </summary>
    Task<IDisposable> WatchWorkspaceChangedAsync(Action<WorkspaceChangedArgs> handler);

    /// <summary>
    /// Subscribes to FocusChanged D-Bus signal.
    /// Signal is emitted when window focus changes.
    /// </summary>
    Task<IDisposable> WatchFocusChangedAsync(Action<FocusChangedArgs> handler);
}
