using Olbrasoft.VirtualAssistant.Core.Models;

namespace Olbrasoft.VirtualAssistant.Desktop.Services;

/// <summary>
/// Interface for desktop monitoring background service.
/// Allows for easier testing with mock implementations.
/// </summary>
public interface IDesktopMonitorBackgroundService
{
    /// <summary>
    /// Observable stream of workspace change events.
    /// </summary>
    IObservable<WorkspaceChangedEventArgs> WorkspaceChanges { get; }

    /// <summary>
    /// Observable stream of focus change events.
    /// </summary>
    IObservable<FocusChangedEventArgs> FocusChanges { get; }

    /// <summary>
    /// Observable stream of complete desktop context updates.
    /// </summary>
    IObservable<DesktopContext> ContextUpdates { get; }

    /// <summary>
    /// Indicates whether desktop monitoring is available (GNOME extension installed).
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets the current desktop context (cached from last update).
    /// </summary>
    DesktopContext? CurrentContext { get; }
}
