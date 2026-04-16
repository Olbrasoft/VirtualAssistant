namespace Olbrasoft.VirtualAssistant.Core.Clipboard;

/// <summary>
/// Interface for clipboard operations on Linux.
/// </summary>
public interface IClipboardManager
{
    /// <summary>
    /// Gets the current clipboard content.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Clipboard content or null if clipboard is empty.</returns>
    Task<string?> GetClipboardAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the clipboard content.
    /// </summary>
    /// <param name="content">Content to set in the clipboard.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetClipboardAsync(string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current X11/Wayland PRIMARY selection (the "mouse middle-click"
    /// selection, read by Shift+Insert in most terminals).
    /// </summary>
    Task<string?> GetPrimarySelectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the X11/Wayland PRIMARY selection. Used when pasting into terminal
    /// agents (e.g. Claude Code) via Shift+Insert, which reads PRIMARY, not CLIPBOARD.
    /// </summary>
    Task SetPrimarySelectionAsync(string content, CancellationToken cancellationToken = default);
}
