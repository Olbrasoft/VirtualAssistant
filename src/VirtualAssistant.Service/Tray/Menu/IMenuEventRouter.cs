namespace Olbrasoft.VirtualAssistant.Service.Tray.Menu;

/// <summary>
/// Routes menu click events to appropriate handlers.
/// Publishes events that external components can subscribe to.
/// </summary>
public interface IMenuEventRouter
{
    /// <summary>
    /// Event fired when user selects Quit from the menu.
    /// </summary>
    event Action? OnQuitRequested;

    /// <summary>
    /// Event fired when user selects Mute/Unmute toggle.
    /// </summary>
    event Action? OnMuteToggleRequested;

    /// <summary>
    /// Event fired when user selects Dashboard menu item.
    /// </summary>
    event Action? OnDashboardRequested;

    /// <summary>
    /// Event fired when user selects About menu item.
    /// </summary>
    event Action? OnAboutRequested;

    /// <summary>
    /// Event fired when user toggles LLM correction.
    /// </summary>
    event Action<bool>? OnLlmCorrectionToggled;

    /// <summary>
    /// Event fired when user wants to reload the Mistral prompt.
    /// </summary>
    event Action? OnReloadPromptRequested;

    /// <summary>
    /// Event fired when user toggles dictation on/off.
    /// </summary>
    event Action<bool>? OnDictationToggleRequested;

    /// <summary>
    /// Event fired when user toggles TTS mute on/off.
    /// </summary>
    event Action<bool>? OnTtsMuteToggleRequested;

    /// <summary>
    /// Handles a menu click event.
    /// </summary>
    /// <param name="id">Menu item ID that was clicked.</param>
    /// <param name="eventId">Event type (e.g., "clicked").</param>
    void HandleMenuEvent(int id, string eventId);
}
