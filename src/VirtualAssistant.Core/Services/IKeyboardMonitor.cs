namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Interface for monitoring keyboard events (e.g., ScrollLock for mute toggle).
/// </summary>
public interface IKeyboardMonitor : IDisposable
{
    /// <summary>
    /// Event raised when a key is pressed.
    /// </summary>
    event EventHandler<KeyEventArgs>? KeyPressed;

    /// <summary>
    /// Event raised when a key is released.
    /// </summary>
    event EventHandler<KeyEventArgs>? KeyReleased;

    /// <summary>
    /// Starts monitoring keyboard events.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel monitoring.</param>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops monitoring keyboard events.
    /// </summary>
    void Stop();

    /// <summary>
    /// Gets whether ScrollLock LED is currently on.
    /// Note: May not work on Wayland, use internal state tracking instead.
    /// </summary>
    bool IsScrollLockOn();
}

/// <summary>
/// Event arguments for keyboard events.
/// </summary>
public class KeyEventArgs : EventArgs
{
    /// <summary>
    /// Gets the key code of the pressed or released key.
    /// </summary>
    public KeyCode Key { get; init; }

    /// <summary>
    /// Gets a value indicating whether the key is pressed (true) or released (false).
    /// </summary>
    public bool IsPressed { get; init; }
}

/// <summary>
/// Common key codes for monitoring.
/// </summary>
public enum KeyCode
{
    /// <summary>Unknown or unsupported key.</summary>
    Unknown = 0,

    /// <summary>Escape key (key code 1).</summary>
    Escape = 1,

    /// <summary>Caps Lock key (key code 58).</summary>
    CapsLock = 58,

    /// <summary>Num Lock key (key code 69).</summary>
    NumLock = 69,

    /// <summary>Scroll Lock key (key code 70).</summary>
    ScrollLock = 70
}
