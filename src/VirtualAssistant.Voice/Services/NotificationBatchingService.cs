using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Batches agent notifications and processes them through humanization and TTS.
/// Uses a sliding window to combine related notifications.
/// </summary>
public class NotificationBatchingService : INotificationBatchingService, IDisposable
{
    private readonly ILogger<NotificationBatchingService> _logger;
    private readonly IHumanizationService _humanizationService;
    private readonly ITtsNotificationService _ttsService;

    private readonly ConcurrentQueue<AgentNotification> _pendingNotifications = new();
    private readonly object _timerLock = new();
    private Timer? _batchTimer;
    private bool _disposed;

    /// <summary>
    /// Time to wait after last notification before processing batch (milliseconds).
    /// </summary>
    private const int BatchWindowMs = 500;

    public NotificationBatchingService(
        ILogger<NotificationBatchingService> logger,
        IHumanizationService humanizationService,
        ITtsNotificationService ttsService)
    {
        _logger = logger;
        _humanizationService = humanizationService;
        _ttsService = ttsService;

        _logger.LogInformation("NotificationBatchingService initialized with {WindowMs}ms batch window", BatchWindowMs);
    }

    public int PendingCount => _pendingNotifications.Count;

    public void QueueNotification(AgentNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        _pendingNotifications.Enqueue(notification);
        _logger.LogDebug("Queued notification: {Agent} {Type}", notification.Agent, notification.Type);

        // Reset/start the batch timer
        ResetBatchTimer();
    }

    private void ResetBatchTimer()
    {
        lock (_timerLock)
        {
            if (_disposed) return;

            // Cancel existing timer and create new one
            _batchTimer?.Dispose();
            _batchTimer = new Timer(
                callback: ProcessBatchCallback,
                state: null,
                dueTime: BatchWindowMs,
                period: Timeout.Infinite);
        }
    }

    private void ProcessBatchCallback(object? state)
    {
        // Fire and forget - exceptions are logged
        _ = ProcessBatchAsync();
    }

    private async Task ProcessBatchAsync()
    {
        try
        {
            // Drain the queue
            var notifications = new List<AgentNotification>();
            while (_pendingNotifications.TryDequeue(out var notification))
            {
                notifications.Add(notification);
            }

            if (notifications.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Processing batch of {Count} notifications", notifications.Count);

            // Humanize the batch
            var humanizedText = await _humanizationService.HumanizeAsync(notifications);

            if (string.IsNullOrWhiteSpace(humanizedText))
            {
                _logger.LogDebug("Humanization returned empty text, skipping TTS");
                return;
            }

            // Speak the humanized text
            await _ttsService.SpeakAsync(humanizedText, source: "assistant");

            _logger.LogInformation("Notification batch processed and spoken: {Text}", humanizedText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing notification batch");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        lock (_timerLock)
        {
            _disposed = true;
            _batchTimer?.Dispose();
            _batchTimer = null;
        }

        GC.SuppressFinalize(this);
    }
}
