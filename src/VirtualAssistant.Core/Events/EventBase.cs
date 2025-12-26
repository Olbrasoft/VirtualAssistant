namespace Olbrasoft.VirtualAssistant.Core.Events;

/// <summary>
/// Base class for all events in the system.
/// Provides common properties like timestamp and correlation ID.
/// </summary>
public abstract record EventBase
{
    /// <summary>
    /// UTC timestamp when the event was created.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Optional correlation ID for tracing related events.
    /// </summary>
    public string? CorrelationId { get; init; }
}
