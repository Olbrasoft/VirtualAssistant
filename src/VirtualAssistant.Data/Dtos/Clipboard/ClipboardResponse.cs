namespace VirtualAssistant.Data.Dtos.Clipboard;

/// <summary>
/// Response model for clipboard operation.
/// </summary>
public class ClipboardResponse
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Status message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
