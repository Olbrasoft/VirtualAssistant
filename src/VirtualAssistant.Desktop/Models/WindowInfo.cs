namespace VirtualAssistant.Desktop.Models;

/// <summary>
/// Represents information about a window.
/// </summary>
/// <param name="Id">Unique window ID</param>
/// <param name="WmClass">Window class (e.g., kitty, microsoft-edge, Code)</param>
/// <param name="Title">Window title</param>
/// <param name="FocusedAt">Timestamp when this window received focus</param>
public record WindowInfo(
    uint Id,
    string WmClass,
    string Title,
    DateTime FocusedAt
);
