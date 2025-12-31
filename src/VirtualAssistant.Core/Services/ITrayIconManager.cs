namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Interface for tray icon manager operations.
/// Provides abstraction over concrete tray icon implementation for dependency injection and testing.
/// </summary>
public interface ITrayIconManager
{
    /// <summary>
    /// Creates a new tray icon.
    /// </summary>
    /// <param name="id">Unique identifier for the icon</param>
    /// <param name="iconPath">Path to the icon file</param>
    /// <param name="tooltip">Tooltip text</param>
    /// <param name="menuHandler">Optional menu handler</param>
    /// <returns>Created tray icon instance</returns>
    Task<ITrayIcon?> CreateIconAsync(string id, string iconPath, string tooltip, ITrayMenuHandler? menuHandler);

    /// <summary>
    /// Removes a tray icon by ID.
    /// </summary>
    /// <param name="id">Icon identifier</param>
    void RemoveIcon(string id);
}

/// <summary>
/// Interface for individual tray icon operations.
/// </summary>
public interface ITrayIcon
{
    /// <summary>
    /// Updates the icon image and tooltip.
    /// </summary>
    /// <param name="iconPath">Path to new icon file</param>
    /// <param name="tooltip">New tooltip text</param>
    void SetIcon(string iconPath, string tooltip);
}

/// <summary>
/// Interface for tray menu handler.
/// </summary>
public interface ITrayMenuHandler
{
}
