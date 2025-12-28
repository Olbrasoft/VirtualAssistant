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
}
