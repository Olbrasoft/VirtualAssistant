namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Reads keyboard LED states (Caps Lock, Scroll Lock, Num Lock).
/// Abstracts platform-specific LED reading logic for testability.
/// </summary>
public interface IKeyboardLedReader
{
    /// <summary>
    /// Checks if Caps Lock is currently on.
    /// </summary>
    /// <returns>True if Caps Lock is on, false otherwise.</returns>
    bool IsCapsLockOn();

    /// <summary>
    /// Checks if Scroll Lock is currently on.
    /// </summary>
    /// <returns>True if Scroll Lock is on, false otherwise.</returns>
    bool IsScrollLockOn();

    /// <summary>
    /// Checks if Num Lock is currently on.
    /// </summary>
    /// <returns>True if Num Lock is on, false otherwise.</returns>
    bool IsNumLockOn();
}
