namespace VirtualAssistant.Data.Dtos.Clipboard;

/// <summary>
/// Request model for clipboard operation.
/// </summary>
public class ClipboardRequest
{
    /// <summary>
    /// Content to copy to clipboard.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
