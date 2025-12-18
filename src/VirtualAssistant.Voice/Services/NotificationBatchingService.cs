using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using VirtualAssistant.Core.Services;
using VirtualAssistant.Data.Enums;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Processes agent notifications and sends them directly to TTS.
/// Notifications are processed immediately as they arrive (no batching delay).
/// Messages are queued and played sequentially - no interruption between agents.
/// Uses speech lock to coordinate with PushToTalk - waits for recording to finish.
/// </summary>
public class NotificationBatchingService : INotificationBatchingService, IDisposable
{
    private readonly ILogger<NotificationBatchingService> _logger;
    private readonly IVirtualAssistantSpeaker _speaker;
    private readonly INotificationService _notificationService;
    private readonly ISpeechLockService _speechLockService;
    private readonly SpeechToTextSettings _speechToTextSettings;

    private readonly ConcurrentQueue<AgentNotification> _pendingNotifications = new();
    private readonly SemaphoreSlim _processingSemaphore = new(1, 1);
    private bool _disposed;

    public NotificationBatchingService(
        ILogger<NotificationBatchingService> logger,
        IVirtualAssistantSpeaker speaker,
        INotificationService notificationService,
        ISpeechLockService speechLockService,
        IOptions<SpeechToTextSettings> speechToTextSettings)
    {
        _logger = logger;
        _speaker = speaker;
        _notificationService = notificationService;
        _speechLockService = speechLockService;
        _speechToTextSettings = speechToTextSettings.Value;

        _logger.LogInformation("NotificationBatchingService initialized (immediate processing, no batch delay)");
    }

    public int PendingCount => _pendingNotifications.Count;

    /// <summary>
    /// Whether notifications are currently being processed.
    /// </summary>
    public bool IsProcessing => _processingSemaphore.CurrentCount == 0;

    public void QueueNotification(AgentNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        _pendingNotifications.Enqueue(notification);
        _logger.LogDebug("Queued notification: {Agent} - {Content}", notification.Agent, notification.Content);

        // Start processing immediately (fire and forget)
        _ = ProcessQueueAsync();
    }

    /// <summary>
    /// Processes all pending notifications sequentially.
    /// Uses semaphore to ensure only one processing loop runs at a time.
    /// </summary>
    private async Task ProcessQueueAsync()
    {
        // Try to acquire the semaphore - if already processing, just return
        // The current processing loop will pick up the new notification
        if (!await _processingSemaphore.WaitAsync(0))
        {
            _logger.LogDebug("Already processing - notification will be picked up by current loop");
            return;
        }

        try
        {
            // Process all notifications in the queue
            while (_pendingNotifications.TryDequeue(out var notification))
            {
                await ProcessSingleNotificationAsync(notification);
            }
        }
        finally
        {
            _processingSemaphore.Release();
        }
    }

    /// <summary>
    /// Processes a single notification: updates status, waits for speech lock, speaks via TTS.
    /// </summary>
    private async Task ProcessSingleNotificationAsync(AgentNotification notification)
    {
        try
        {
            _logger.LogInformation("Processing notification from {Agent}", notification.Agent);

            // Update status to Processing
            if (notification.NotificationId.HasValue)
            {
                await _notificationService.UpdateStatusAsync(
                    new[] { notification.NotificationId.Value },
                    NotificationStatusEnum.Processing);
            }

            var text = notification.Content;

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogDebug("Empty notification text, skipping TTS");
                if (notification.NotificationId.HasValue)
                {
                    await _notificationService.UpdateStatusAsync(
                        new[] { notification.NotificationId.Value },
                        NotificationStatusEnum.Played);
                }
                return;
            }

            // Wait for speech lock to be released before speaking
            await WaitForSpeechUnlockAsync();

            // Speak the text directly
            await _speaker.SpeakAsync(text, notification.Agent);

            // Update status to Played after successful TTS
            if (notification.NotificationId.HasValue)
            {
                await _notificationService.UpdateStatusAsync(
                    new[] { notification.NotificationId.Value },
                    NotificationStatusEnum.Played);
            }

            _logger.LogInformation("Notification sent to TTS: {Text}", text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing notification from {Agent}", notification.Agent);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _processingSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Immediately processes any pending notifications.
    /// Since we now process immediately, this just ensures the queue is being processed.
    /// </summary>
    public async Task FlushAsync()
    {
        if (_pendingNotifications.IsEmpty)
        {
            _logger.LogDebug("FlushAsync called but no pending notifications");
            return;
        }

        _logger.LogInformation("Flushing {Count} pending notifications", _pendingNotifications.Count);
        await ProcessQueueAsync();
    }

    /// <summary>
    /// Waits for speech lock to be released before playing notification.
    /// </summary>
    private async Task WaitForSpeechUnlockAsync()
    {
        if (!_speechLockService.IsLocked)
        {
            return;
        }

        _logger.LogInformation("🎤 Speech lock active - waiting before playing notification...");

        while (_speechLockService.IsLocked)
        {
            await Task.Delay(_speechToTextSettings.PollingIntervalMs);
        }

        _logger.LogInformation("🔊 Speech lock released - proceeding with notification");
    }
}
