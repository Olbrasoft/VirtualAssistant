namespace VirtualAssistant.Core.Services;

/// <summary>
/// Service for batching agent notifications before humanization.
/// Collects notifications over a short time window to combine related messages.
/// Supports concurrent notification detection with speech cancellation.
/// </summary>
public interface INotificationBatchingService
{
    /// <summary>
    /// Queues a notification for batching.
    /// The notification will be humanized and spoken after the batch window expires.
    /// If a batch is currently being processed, cancels current speech.
    /// </summary>
    /// <param name="notification">Notification to queue</param>
    void QueueNotification(AgentNotification notification);

    /// <summary>
    /// Gets the current batch size (for monitoring/testing).
    /// </summary>
    int PendingCount { get; }

    /// <summary>
    /// Whether a batch is currently being processed (humanization + TTS).
    /// </summary>
    bool IsProcessing { get; }
}
