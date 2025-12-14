using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using VirtualAssistant.Core.Services;
using VirtualAssistant.Data.Enums;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Batches agent notifications and processes them through humanization and TTS.
/// Uses a sliding window to combine related notifications.
/// Detects concurrent notifications and cancels current speech to include new messages.
/// </summary>
public class NotificationBatchingService : INotificationBatchingService, IDisposable
{
    private readonly ILogger<NotificationBatchingService> _logger;
    private readonly IHumanizationService _humanizationService;
    private readonly IVirtualAssistantSpeaker _speaker;
    private readonly INotificationService _notificationService;

    private readonly ConcurrentQueue<AgentNotification> _pendingNotifications = new();
    private readonly object _timerLock = new();
    private Timer? _batchTimer;
    private bool _disposed;
    private volatile bool _isProcessing;

    /// <summary>
    /// Time to wait after last notification before processing batch (milliseconds).
    /// </summary>
    private const int BatchWindowMs = 3000;

    public NotificationBatchingService(
        ILogger<NotificationBatchingService> logger,
        IHumanizationService humanizationService,
        IVirtualAssistantSpeaker speaker,
        INotificationService notificationService)
    {
        _logger = logger;
        _humanizationService = humanizationService;
        _speaker = speaker;
        _notificationService = notificationService;

        _logger.LogInformation("NotificationBatchingService initialized with {WindowMs}ms batch window", BatchWindowMs);
    }

    public int PendingCount => _pendingNotifications.Count;

    /// <summary>
    /// Whether a batch is currently being processed.
    /// </summary>
    public bool IsProcessing => _isProcessing;

    public void QueueNotification(AgentNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        _pendingNotifications.Enqueue(notification);
        _logger.LogDebug("Queued notification: {Agent} {Type}", notification.Agent, notification.Type);

        // If currently processing, cancel speech and restart timer
        // This ensures new notifications are included in the next batch
        if (_isProcessing)
        {
            _logger.LogInformation("Concurrent notification detected - cancelling current speech");
            _speaker.CancelCurrentSpeech();
        }

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
        _isProcessing = true;
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

            // Get notification IDs for status updates
            var notificationIds = notifications
                .Where(n => n.NotificationId.HasValue)
                .Select(n => n.NotificationId!.Value)
                .ToList();

            // Update status to Processing
            if (notificationIds.Count > 0)
            {
                await _notificationService.UpdateStatusAsync(notificationIds, NotificationStatusEnum.Processing);
            }

            // Update status to SentForSummarization
            if (notificationIds.Count > 0)
            {
                await _notificationService.UpdateStatusAsync(notificationIds, NotificationStatusEnum.SentForSummarization);
            }

            // Humanize the batch
            var humanizedText = await _humanizationService.HumanizeAsync(notifications);

            // Update status to Summarized
            if (notificationIds.Count > 0)
            {
                await _notificationService.UpdateStatusAsync(notificationIds, NotificationStatusEnum.Summarized);
            }

            if (string.IsNullOrWhiteSpace(humanizedText))
            {
                _logger.LogDebug("Humanization returned empty text, skipping TTS");
                // Mark as Played even without speech (nothing to play)
                if (notificationIds.Count > 0)
                {
                    await _notificationService.UpdateStatusAsync(notificationIds, NotificationStatusEnum.Played);
                }
                return;
            }

            // Speak the humanized text (with workspace detection)
            var agentName = notifications.FirstOrDefault()?.Agent;
            await _speaker.SpeakAsync(humanizedText, agentName);

            // Update status to Played after successful TTS
            if (notificationIds.Count > 0)
            {
                await _notificationService.UpdateStatusAsync(notificationIds, NotificationStatusEnum.Played);
            }

            _logger.LogInformation("Notification batch processed: {Text}", humanizedText);

            // Check if new notifications arrived during speech
            // If so, they will be processed in the next batch (timer was reset)
            if (_pendingNotifications.Count > 0)
            {
                _logger.LogDebug("New notifications arrived during processing, will be handled in next batch");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing notification batch");
        }
        finally
        {
            _isProcessing = false;
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
