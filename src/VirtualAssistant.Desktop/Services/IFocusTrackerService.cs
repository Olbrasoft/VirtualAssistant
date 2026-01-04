using VirtualAssistant.Core.Models;

namespace VirtualAssistant.Desktop.Services;

/// <summary>
/// Interface for tracking desktop state via focus-tracker GNOME extension.
/// </summary>
public interface IFocusTrackerService : IAsyncDisposable
{
    /// <summary>
    /// Gets the current desktop context (workspace, window, application).
    /// </summary>
    Task<DesktopContext> GetCurrentContextAsync(CancellationToken cancellationToken = default);
}
