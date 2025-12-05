using VirtualAssistant.Desktop.Events;
using VirtualAssistant.Desktop.Models;

namespace VirtualAssistant.Desktop.Services;

/// <summary>
/// Monitors window focus changes and tracks current and previous windows.
/// </summary>
public interface IWindowFocusMonitor
{
    /// <summary>
    /// Currently focused window, or null if unknown.
    /// </summary>
    WindowInfo? CurrentWindow { get; }

    /// <summary>
    /// Previously focused window, or null if no previous window.
    /// </summary>
    WindowInfo? PreviousWindow { get; }

    /// <summary>
    /// Fired when window focus changes.
    /// </summary>
    event EventHandler<WindowFocusChangedEventArgs>? FocusChanged;

    /// <summary>
    /// Start monitoring window focus.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop monitoring.</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop monitoring window focus.
    /// </summary>
    Task StopAsync();
}
