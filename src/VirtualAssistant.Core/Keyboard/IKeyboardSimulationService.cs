namespace Olbrasoft.VirtualAssistant.Core.Keyboard;

/// <summary>
/// Service for simulating keyboard input into the active window.
/// Used for dictation to insert transcribed text into the user's current application.
/// </summary>
public interface IKeyboardSimulationService
{
    /// <summary>
    /// Types the specified text into the currently active window by simulating keyboard input.
    /// </summary>
    /// <param name="text">Text to type into the active window.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if text was typed successfully, false otherwise.</returns>
    Task<bool> TypeIntoActiveWindowAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a key press to the active window using dotool.
    /// </summary>
    /// <param name="key">Key to send (e.g. "enter", "ctrl+u").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendKeyAsync(string key, CancellationToken cancellationToken = default);
}
