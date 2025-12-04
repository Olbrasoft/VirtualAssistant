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
    public KeyCode Key { get; init; }
    public bool IsPressed { get; init; }
}

/// <summary>
/// Common key codes for monitoring.
/// </summary>
public enum KeyCode
{
    Unknown = 0,
    CapsLock = 58,
    ScrollLock = 70,
    NumLock = 69,
    Escape = 1
}
