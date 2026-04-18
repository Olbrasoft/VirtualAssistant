namespace Olbrasoft.VirtualAssistant.Core.Events;

/// <summary>
/// Event bus for decoupled inter-worker communication.
/// Implements Observer pattern for asynchronous event publishing.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event to all registered handlers.
    /// Handlers are invoked asynchronously and in parallel.
    /// </summary>
    /// <param name="event">The event to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class;

    /// <summary>
    /// Subscribes a handler to events of type TEvent. Invocation order across
    /// multiple handlers is NOT guaranteed — <see cref="PublishAsync"/> runs
    /// them in parallel via <c>Task.WhenAll</c>, so subscribers must not rely
    /// on their relative ordering. (Earlier wording claimed registration
    /// order was preserved; that was never true of the parallel dispatch and
    /// is removed to match the implementation.)
    /// </summary>
    /// <param name="handler">The handler to invoke when event is published.</param>
    /// <returns>Subscription token for unsubscribing.</returns>
    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : class;
}
