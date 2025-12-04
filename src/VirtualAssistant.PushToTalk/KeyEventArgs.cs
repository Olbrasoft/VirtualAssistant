namespace Olbrasoft.VirtualAssistant.PushToTalk;

/// <summary>
/// Event arguments for keyboard events.
/// </summary>
public class KeyEventArgs : EventArgs
{
    /// <summary>
    /// Gets the key code.
    /// </summary>
    public KeyCode Key { get; }

    /// <summary>
    /// Gets the raw key code value.
    /// </summary>
    public int RawKeyCode { get; }

    /// <summary>
    /// Gets the timestamp when the key event occurred.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyEventArgs"/> class.
    /// </summary>
    /// <param name="key">Key code.</param>
    /// <param name="rawKeyCode">Raw key code value.</param>
    /// <param name="timestamp">Event timestamp.</param>
    public KeyEventArgs(KeyCode key, int rawKeyCode, DateTime timestamp)
    {
        Key = key;
        RawKeyCode = rawKeyCode;
        Timestamp = timestamp;
    }
}
