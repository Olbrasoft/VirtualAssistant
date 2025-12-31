namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Handles state synchronization and notification between services.
/// Implements Observer pattern - subscribes to state changes and updates UI accordingly.
/// </summary>
public interface IStateNotificationHandler
{
    /// <summary>
    /// Subscribes to state change events (mute, dictation).
    /// </summary>
    void SubscribeToEvents();

    /// <summary>
    /// Unsubscribes from state change events.
    /// </summary>
    void UnsubscribeFromEvents();

    /// <summary>
    /// Initializes states on startup (mute, dictation, TTS mute, service status).
    /// </summary>
    Task InitializeStatesAsync();
}
