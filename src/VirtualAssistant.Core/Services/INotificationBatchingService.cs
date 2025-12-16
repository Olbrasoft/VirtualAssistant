namespace VirtualAssistant.Core.Services;

/// <summary>
/// Service for batching agent notifications before humanization.
/// Collects notifications over a short time window to combine related messages.
/// Messages are queued and played sequentially - no interruption between agents.
/// </summary>
public interface INotificationBatchingService
{
    /// <summary>
    /// Queues a notification for batching.
    /// The notification will be humanized and spoken after the batch window expires.
    /// If a batch is currently being processed, the notification waits in queue.
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

    /// <summary>
    /// Immediately processes any pending notifications without waiting for the batch timer.
    /// Used when speech lock is released to play queued messages right away.
    /// </summary>
    /// <returns>Task that completes when the batch has been processed.</returns>
    Task FlushAsync();
}
