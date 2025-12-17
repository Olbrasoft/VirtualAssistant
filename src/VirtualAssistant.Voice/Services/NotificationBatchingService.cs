using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtualAssistant.Core.Services;
using VirtualAssistant.Data.Enums;
using VirtualAssistant.GitHub.Configuration;
using VirtualAssistant.GitHub.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Batches agent notifications and processes them through humanization and TTS.
/// Uses a sliding window to combine related notifications.
/// Messages are queued and played sequentially - no interruption between agents.
/// </summary>
public class NotificationBatchingService : INotificationBatchingService, IDisposable
{
    private readonly ILogger<NotificationBatchingService> _logger;
    private readonly IHumanizationService _humanizationService;
    private readonly IVirtualAssistantSpeaker _speaker;
    private readonly INotificationService _notificationService;
    private readonly IIssueSummaryClient _issueSummaryClient;
    private readonly GitHubSettings _gitHubSettings;

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
        INotificationService notificationService,
        IIssueSummaryClient issueSummaryClient,
        IOptions<GitHubSettings> gitHubSettings)
    {
        _logger = logger;
        _humanizationService = humanizationService;
        _speaker = speaker;
        _notificationService = notificationService;
        _issueSummaryClient = issueSummaryClient;
        _gitHubSettings = gitHubSettings.Value;

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

            // Update status to SentForSummarization
            if (notificationIds.Count > 0)
            {
                await _notificationService.UpdateStatusAsync(notificationIds, NotificationStatusEnum.SentForSummarization);
            }

            // Fetch issue summaries if notifications have associated issues
            IReadOnlyDictionary<int, IssueSummaryInfo>? issueSummaries = null;
            if (notificationIds.Count > 0)
            {
                issueSummaries = await FetchIssueSummariesAsync(notificationIds);
            }

            string? humanizedText;

            // Skip humanization if no associated issues - send text directly to TTS
            // This is faster and saves LLM API calls for simple status notifications
            if (issueSummaries is null or { Count: 0 })
            {
                _logger.LogDebug("No associated issues, skipping humanization - sending text directly to TTS");
                humanizedText = string.Join(" ", notifications.Select(n => n.Content));
            }
            else
            {
                // Humanize the batch with issue context
                humanizedText = await _humanizationService.HumanizeAsync(notifications, issueSummaries);
            }

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
    /// Fetches Czech issue summaries for notifications with associated GitHub issues.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, IssueSummaryInfo>?> FetchIssueSummariesAsync(
        IReadOnlyList<int> notificationIds)
    {
        try
        {
            // Get associated issue IDs
            var issueIds = await _notificationService.GetAssociatedIssueIdsAsync(notificationIds);
            if (issueIds.Count == 0)
            {
                _logger.LogDebug("No associated GitHub issues for these notifications");
                return null;
            }

            _logger.LogDebug("Fetching summaries for {Count} associated GitHub issues", issueIds.Count);

            // Check if we have required configuration
            if (string.IsNullOrEmpty(_gitHubSettings.Owner) || string.IsNullOrEmpty(_gitHubSettings.DefaultRepo))
            {
                _logger.LogWarning("GitHub Owner or DefaultRepo not configured, cannot fetch issue summaries");
                return null;
            }

            // Fetch summaries from GitHub.Issues API
            var result = await _issueSummaryClient.GetSummariesAsync(
                _gitHubSettings.Owner,
                _gitHubSettings.DefaultRepo,
                issueIds);

            if (!string.IsNullOrEmpty(result.Error))
            {
                _logger.LogWarning("Failed to fetch issue summaries: {Error}", result.Error);
                return null;
            }

            if (result.Summaries.Count == 0)
            {
                _logger.LogDebug("No summaries returned from API");
                return null;
            }

            // Convert to IssueSummaryInfo dictionary
            var summaries = result.Summaries.ToDictionary(
                kvp => kvp.Key,
                kvp => new IssueSummaryInfo
                {
                    IssueNumber = kvp.Value.IssueNumber,
                    CzechTitle = kvp.Value.CzechTitle,
                    CzechSummary = kvp.Value.CzechSummary,
                    IsOpen = kvp.Value.IsOpen
                });

            _logger.LogInformation("Fetched {Count} issue summaries for humanization context", summaries.Count);
            return summaries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching issue summaries, continuing without context");
            return null;
        }
    }
}
