namespace Olbrasoft.VirtualAssistant.Core.Events;

/// <summary>
/// Event bus for decoupled publish/subscribe communication between components.
/// Implements Observer pattern with centralized event routing.
/// </summary>
/// <remarks>
/// Benefits:
/// - Loose coupling - subscribers don't need to know publishers
/// - Easy testing - can mock event bus
/// - Thread-safe - handles concurrent publish/subscribe
/// - Async support - non-blocking event delivery
/// </remarks>
public interface IEventBus
{
    /// <summary>
    /// Subscribes to events of type TEvent.
    /// </summary>
    /// <typeparam name="TEvent">Type of event to subscribe to.</typeparam>
    /// <param name="handler">Handler to invoke when event is published.</param>
    /// <returns>Disposable subscription that unsubscribes when disposed.</returns>
    /// <example>
    /// <code>
    /// using var subscription = eventBus.Subscribe&lt;MuteStateChangedEvent&gt;(e =>
    /// {
    ///     Console.WriteLine($"Mute changed: {e.IsMuted}");
    /// });
    /// </code>
    /// </example>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;

    /// <summary>
    /// Publishes an event to all subscribers asynchronously.
    /// </summary>
    /// <typeparam name="TEvent">Type of event to publish.</typeparam>
    /// <param name="event">Event instance to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes when all handlers have been invoked.</returns>
    /// <example>
    /// <code>
    /// await eventBus.PublishAsync(new MuteStateChangedEvent(true));
    /// </code>
    /// </example>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class;
}
