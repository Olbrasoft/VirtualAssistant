namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Handles state change notifications from various services and coordinates UI updates.
/// Implements Observer pattern for state synchronization.
/// </summary>
public interface IStateNotificationHandler
{
    /// <summary>
    /// Subscribes to state change events from services.
    /// </summary>
    void SubscribeToEvents();

    /// <summary>
    /// Unsubscribes from state change events.
    /// </summary>
    void UnsubscribeFromEvents();

    /// <summary>
    /// Initializes states on startup (mute, dictation, TTS).
    /// </summary>
    Task InitializeStatesAsync();
}
