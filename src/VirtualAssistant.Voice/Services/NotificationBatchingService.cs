using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;

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
    private readonly ISpeechController _speechController;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISpeechLockService _speechLockService;
    private readonly SpeechToTextSettings _speechToTextSettings;

    private readonly ConcurrentQueue<AgentNotification> _pendingNotifications = new();
    private readonly SemaphoreSlim _processingSemaphore = new(1, 1);
    private bool _disposed;

    public NotificationBatchingService(
        ILogger<NotificationBatchingService> logger,
        ISpeechController speechController,
        IServiceScopeFactory scopeFactory,
        ISpeechLockService speechLockService,
        IOptions<SpeechToTextSettings> speechToTextSettings)
    {
        _logger = logger;
        _speechController = speechController;
        _scopeFactory = scopeFactory;
        _speechLockService = speechLockService;
        _speechToTextSettings = speechToTextSettings.Value;

        _logger.LogInformation("NotificationBatchingService initialized (immediate processing, no batch delay)");
    }

    public int PendingCount => _pendingNotifications.Count;

    /// <summary>
    /// Whether notifications are currently being processed.
    /// </summary>
    public bool IsProcessing => _processingSemaphore.CurrentCount == 0;

    /// <summary>
    /// Queues a notification for processing and immediately starts processing.
    /// </summary>
    /// <param name="notification">The notification to queue.</param>
    /// <exception cref="ArgumentNullException">Thrown when notification is null.</exception>
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

            var text = notification.Content;
            var notificationId = notification.NotificationId;

            // Short-circuit empty text before opening any scope — no DB work to do
            // beyond MarkAsPlayed when we have an id.
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogDebug("Empty notification text, skipping TTS");
                if (notificationId.HasValue)
                {
                    await InvokeTrackerAsync(t => t.MarkAsPlayedAsync(notificationId.Value));
                }
                return;
            }

            // Phase 1 (pre-TTS): open a short-lived scope to mark Processing.
            if (notificationId.HasValue)
            {
                await InvokeTrackerAsync(t => t.MarkAsProcessingAsync(notificationId.Value));
            }

            await WaitForSpeechUnlockAsync();

            var ttsResult = await _speechController.SpeakAsync(text, notification.Agent, skipCache: true);

            // Phase 2 (post-TTS): second short-lived scope for outcome + played updates.
            // Splitting the scopes means the scoped DbContext is not held across the
            // full TTS operation (which can take seconds).
            if (notificationId.HasValue)
            {
                var status = ttsResult.Cancelled ? "cancelled_by_dictation"
                    : ttsResult.Skipped ? "skipped"
                    : ttsResult.Success ? "success"
                    : "error";

                await InvokeTrackerAsync(async t =>
                {
                    await t.RecordTtsOutcomeAsync(
                        notificationId.Value,
                        ttsResult.ProviderUsed,
                        status,
                        ttsResult.DurationMs);

                    if (ttsResult.Success || ttsResult.Skipped)
                    {
                        await t.MarkAsPlayedAsync(notificationId.Value);
                    }
                });
            }

            _logger.LogInformation("Notification sent to TTS: {Text} (Provider: {Provider}, Status: {Status})",
                text, ttsResult.ProviderUsed ?? "none", ttsResult.Success ? "success" : "failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing notification from {Agent}", notification.Agent);
        }
    }

    /// <summary>
    /// Resolves <see cref="INotificationTracker"/> inside a fresh DI scope so the
    /// scoped DbContext behind it is released as soon as <paramref name="action"/>
    /// returns. Avoids keeping scoped state alive across the long TTS operation.
    /// </summary>
    private async Task InvokeTrackerAsync(Func<INotificationTracker, Task> action)
    {
        using var scope = _scopeFactory.CreateScope();
        var tracker = scope.ServiceProvider.GetRequiredService<INotificationTracker>();
        await action(tracker);
    }

    /// <summary>
    /// Releases resources used by the notification batching service, including the processing semaphore.
    /// </summary>
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
