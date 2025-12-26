using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Olbrasoft.VirtualAssistant.Core.Events;

/// <summary>
/// In-memory implementation of IEventBus.
/// Thread-safe, asynchronous event publishing with parallel handler execution.
/// </summary>
public class InMemoryEventBus : IEventBus
{
    private readonly ILogger<InMemoryEventBus> _logger;
    private readonly ConcurrentDictionary<Type, ConcurrentBag<Delegate>> _handlers = new();

    public InMemoryEventBus(ILogger<InMemoryEventBus> logger)
    {
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        var eventType = typeof(TEvent);

        if (!_handlers.TryGetValue(eventType, out var handlers) || handlers.IsEmpty)
        {
            _logger.LogDebug("No handlers registered for event {EventType}", eventType.Name);
            return;
        }

        _logger.LogDebug("Publishing event {EventType} to {HandlerCount} handlers", eventType.Name, handlers.Count);

        var tasks = handlers
            .Cast<Func<TEvent, CancellationToken, Task>>()
            .Select(handler => InvokeHandlerAsync(handler, @event, cancellationToken));

        await Task.WhenAll(tasks);
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : class
    {
        var eventType = typeof(TEvent);
        var handlers = _handlers.GetOrAdd(eventType, _ => new ConcurrentBag<Delegate>());
        handlers.Add(handler);

        _logger.LogDebug("Registered handler for event {EventType} (total: {HandlerCount})", eventType.Name, handlers.Count);

        return new Subscription<TEvent>(this, handler);
    }

    private async Task InvokeHandlerAsync<TEvent>(
        Func<TEvent, CancellationToken, Task> handler,
        TEvent @event,
        CancellationToken cancellationToken)
        where TEvent : class
    {
        try
        {
            await handler(@event, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in event handler for {EventType}", typeof(TEvent).Name);
        }
    }

    private void Unsubscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : class
    {
        var eventType = typeof(TEvent);
        if (!_handlers.TryGetValue(eventType, out var handlers))
            return;

        // ConcurrentBag doesn't support removal, so we create a new bag without the handler
        var newHandlers = new ConcurrentBag<Delegate>(
            handlers.Where(h => !ReferenceEquals(h, handler)));

        _handlers.TryUpdate(eventType, newHandlers, handlers);

        _logger.LogDebug("Unregistered handler for event {EventType} (remaining: {HandlerCount})",
            eventType.Name, newHandlers.Count);
    }

    private class Subscription<TEvent> : IDisposable
        where TEvent : class
    {
        private readonly InMemoryEventBus _eventBus;
        private readonly Func<TEvent, CancellationToken, Task> _handler;
        private bool _disposed;

        public Subscription(InMemoryEventBus eventBus, Func<TEvent, CancellationToken, Task> handler)
        {
            _eventBus = eventBus;
            _handler = handler;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _eventBus.Unsubscribe(_handler);
            _disposed = true;
        }
    }
}
