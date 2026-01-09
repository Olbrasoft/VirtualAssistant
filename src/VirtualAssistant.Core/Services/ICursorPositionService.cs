namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Service for querying cursor position for overlay placement.
/// Abstracts the underlying position source (GNOME extension, AT-SPI, etc.).
/// </summary>
public interface ICursorPositionService
{
    /// <summary>
    /// Gets the best available cursor position for overlay placement.
    /// </summary>
    /// <returns>Screen coordinates (X, Y), or null if unavailable.</returns>
    Task<(int X, int Y)?> GetCursorPositionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the geometry of the currently focused window.
    /// </summary>
    /// <returns>Window geometry (X, Y, Width, Height), or null if unavailable.</returns>
    Task<(int X, int Y, int Width, int Height)?> GetActiveWindowGeometryAsync(CancellationToken cancellationToken = default);
}
