using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using VirtualAssistant.Core.Services;
using VirtualAssistant.Data.Enums;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Batches agent notifications and sends them directly to TTS.
/// Uses a sliding window to combine related notifications.
/// Messages are queued and played sequentially - no interruption between agents.
/// Uses speech lock to coordinate with PushToTalk - waits for recording to finish.
/// No LLM humanization - text goes directly to TTS as received from agents.
/// </summary>
public class NotificationBatchingService : INotificationBatchingService, IDisposable
{
    private readonly ILogger<NotificationBatchingService> _logger;
    private readonly IVirtualAssistantSpeaker _speaker;
    private readonly INotificationService _notificationService;
    private readonly ISpeechLockService _speechLockService;
    private readonly SpeechToTextSettings _speechToTextSettings;

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

        _logger.LogInformation("NotificationBatchingService initialized with {WindowMs}ms batch window (direct TTS, no humanization)",
            BatchWindowMs);
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

        // If currently processing, just queue - don't interrupt current speech
        // Agent messages should play sequentially, never interrupt each other
        if (_isProcessing)
        {
            _logger.LogDebug("Notification queued during processing - will play after current message");
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

            // Combine notification texts directly - no LLM humanization
            // Text from Claude Code/OpenRouter is already well-formatted
            var text = string.Join(" ", notifications.Select(n => n.Content));

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogDebug("Empty notification text, skipping TTS");
                if (notificationIds.Count > 0)
                {
                    await _notificationService.UpdateStatusAsync(notificationIds, NotificationStatusEnum.Played);
                }
                return;
            }

            // Wait for speech lock to be released before speaking
            // This ensures we don't interrupt user's dictation
            await WaitForSpeechUnlockAsync();

            // Speak the text directly (with workspace detection)
            var agentName = notifications.FirstOrDefault()?.Agent;
            await _speaker.SpeakAsync(text, agentName);

            // Update status to Played after successful TTS
            if (notificationIds.Count > 0)
            {
                await _notificationService.UpdateStatusAsync(notificationIds, NotificationStatusEnum.Played);
            }

            _logger.LogInformation("Notification sent to TTS: {Text}", text);

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

    /// <summary>
    /// Immediately processes any pending notifications without waiting for the batch timer.
    /// Used when speech lock is released to play queued messages right away.
    /// </summary>
    public async Task FlushAsync()
    {
        if (_pendingNotifications.IsEmpty)
        {
            _logger.LogDebug("FlushAsync called but no pending notifications");
            return;
        }

        // Cancel the batch timer since we're processing immediately
        lock (_timerLock)
        {
            _batchTimer?.Dispose();
            _batchTimer = null;
        }

        _logger.LogInformation("Flushing {Count} pending notifications", _pendingNotifications.Count);
        await ProcessBatchAsync();
    }

    /// <summary>
    /// Waits for speech lock to be released before playing notification.
    /// Uses speech lock service as the single source of truth.
    /// PushToTalk calls /api/speech-lock/start when recording begins and /stop when it ends.
    /// </summary>
    private async Task WaitForSpeechUnlockAsync()
    {
        // Check speech lock - this is set by PushToTalk via API
        if (!_speechLockService.IsLocked)
        {
            return;
        }

        _logger.LogInformation("🎤 Speech lock active - waiting before playing notification...");

        // Poll until lock is released
        while (_speechLockService.IsLocked)
        {
            await Task.Delay(_speechToTextSettings.PollingIntervalMs);
        }

        _logger.LogInformation("🔊 Speech lock released - proceeding with notification");
    }
}
