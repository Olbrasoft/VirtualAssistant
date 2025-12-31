namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Discovers keyboard input devices on the system.
/// Abstracts platform-specific device discovery logic for testability.
/// </summary>
public interface IKeyboardDeviceDiscovery
{
    /// <summary>
    /// Finds the primary keyboard device path.
    /// </summary>
    /// <returns>Device path (e.g., "/dev/input/event3" on Linux).</returns>
    string FindKeyboardDevice();

    /// <summary>
    /// Checks if the specified device is a keyboard.
    /// </summary>
    /// <param name="devicePath">Device path to check.</param>
    /// <returns>True if device is a keyboard, false otherwise.</returns>
    bool IsKeyboardDevice(string devicePath);
}
