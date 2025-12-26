using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Olbrasoft.VirtualAssistant.Core.Events;

/// <summary>
/// Thread-safe implementation of IEventBus using concurrent dictionaries.
/// Supports multiple subscribers per event type.
/// </summary>
public class EventBus : IEventBus
{
    private readonly ILogger<EventBus> _logger;

    // Dictionary of event type -> list of handlers
    // ConcurrentDictionary ensures thread-safe access
    private readonly ConcurrentDictionary<Type, ConcurrentBag<Delegate>> _subscriptions = new();

    public EventBus(ILogger<EventBus> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(TEvent);
        var handlers = _subscriptions.GetOrAdd(eventType, _ => new ConcurrentBag<Delegate>());
        handlers.Add(handler);

        _logger.LogDebug("Subscribed to {EventType} (total subscribers: {Count})",
            eventType.Name, handlers.Count);

        // Return disposable that removes the handler when disposed
        return new Subscription<TEvent>(this, handler);
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class
    {
        if (@event == null) throw new ArgumentNullException(nameof(@event));

        var eventType = typeof(TEvent);

        if (!_subscriptions.TryGetValue(eventType, out var handlers) || handlers.IsEmpty)
        {
            _logger.LogTrace("No subscribers for {EventType}", eventType.Name);
            return;
        }

        _logger.LogDebug("Publishing {EventType} to {Count} subscribers",
            eventType.Name, handlers.Count);

        // Invoke all handlers concurrently
        var tasks = handlers
            .OfType<Action<TEvent>>()
            .Select(handler => Task.Run(() =>
            {
                try
                {
                    handler(@event);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in event handler for {EventType}", eventType.Name);
                }
            }, cancellationToken));

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Removes a handler from subscriptions.
    /// </summary>
    private void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : class
    {
        var eventType = typeof(TEvent);

        if (!_subscriptions.TryGetValue(eventType, out var handlers))
        {
            return;
        }

        // Create new bag without the handler
        // ConcurrentBag doesn't support removal, so we recreate it
        var newHandlers = new ConcurrentBag<Delegate>(
            handlers.Where(h => !ReferenceEquals(h, handler)));

        _subscriptions.TryUpdate(eventType, newHandlers, handlers);

        _logger.LogDebug("Unsubscribed from {EventType} (remaining subscribers: {Count})",
            eventType.Name, newHandlers.Count);
    }

    /// <summary>
    /// Disposable subscription handle.
    /// </summary>
    private class Subscription<TEvent> : IDisposable where TEvent : class
    {
        private readonly EventBus _eventBus;
        private readonly Action<TEvent> _handler;
        private bool _disposed;

        public Subscription(EventBus eventBus, Action<TEvent> handler)
        {
            _eventBus = eventBus;
            _handler = handler;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _eventBus.Unsubscribe(_handler);
            _disposed = true;
        }
    }
}
