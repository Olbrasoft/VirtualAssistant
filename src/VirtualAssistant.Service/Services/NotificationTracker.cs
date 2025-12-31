using Microsoft.Extensions.Logging;
using VirtualAssistant.Core.Services;
using VirtualAssistant.Data.Enums;

namespace Olbrasoft.VirtualAssistant.Service.Services;

/// <summary>
/// Tracks notification lifecycle and handles database status updates.
/// Encapsulates all notification state transitions in a single service.
/// </summary>
public class NotificationTracker : INotificationTracker
{
    private readonly ILogger<NotificationTracker> _logger;
    private readonly INotificationService _notificationService;

    public NotificationTracker(
        ILogger<NotificationTracker> logger,
        INotificationService notificationService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
    }

    /// <summary>
    /// Marks a notification as being processed.
    /// </summary>
    public async Task MarkAsProcessingAsync(int notificationId)
    {
        _logger.LogDebug("Marking notification {NotificationId} as Processing", notificationId);
        await _notificationService.UpdateStatusAsync(
            new[] { notificationId },
            NotificationStatusEnum.Processing);
    }

    /// <summary>
    /// Marks a notification as played successfully.
    /// </summary>
    public async Task MarkAsPlayedAsync(int notificationId)
    {
        _logger.LogDebug("Marking notification {NotificationId} as Played", notificationId);
        await _notificationService.UpdateStatusAsync(
            new[] { notificationId },
            NotificationStatusEnum.Played);
    }

    /// <summary>
    /// Records a TTS attempt outcome for tracking purposes.
    /// </summary>
    public async Task RecordTtsOutcomeAsync(int notificationId, string? provider, string status, int? durationMs)
    {
        _logger.LogDebug("Recording TTS outcome for notification {NotificationId}: Provider={Provider}, Status={Status}, Duration={Duration}ms",
            notificationId, provider ?? "none", status, durationMs);

        await _notificationService.RecordTtsOutcomeAsync(
            notificationId,
            provider,
            status,
            durationMs);
    }
}
