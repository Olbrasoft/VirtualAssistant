using Olbrasoft.VirtualAssistant.Core.Models;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Service for tracking user's desktop context (workspace, focused window, active application).
/// </summary>
public interface IDesktopContextService
{
    /// <summary>
    /// Gets current desktop context (workspace, focused window, active app).
    /// Returns cached value if LinuxDesktop unavailable.
    /// </summary>
    Task<DesktopContext> GetCurrentContextAsync(CancellationToken ct = default);

    /// <summary>
    /// Observable stream of context changes (workspace switches, focus changes).
    /// </summary>
    IObservable<DesktopContextChange> ContextChanges { get; }

    /// <summary>
    /// Checks if desktop monitoring is available (GNOME extensions running).
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
