namespace Olbrasoft.VirtualAssistant.Core.WindowManagement;

/// <summary>
/// Interface for detecting if the active window is a terminal application.
/// Used to determine the correct paste keyboard shortcut (Ctrl+V vs Ctrl+Shift+V).
/// </summary>
public interface ITerminalDetector
{
    /// <summary>
    /// Checks if the currently active window is a terminal application.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the active window is a terminal, false otherwise.</returns>
    Task<bool> IsTerminalActiveAsync(CancellationToken cancellationToken = default);
}
