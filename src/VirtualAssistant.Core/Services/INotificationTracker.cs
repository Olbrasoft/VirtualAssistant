namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Encapsulates notification lifecycle tracking and database status updates.
/// Centralizes all notification status transitions in one place.
/// </summary>
public interface INotificationTracker
{
    /// <summary>
    /// Marks a notification as being processed.
    /// </summary>
    /// <param name="notificationId">The notification ID.</param>
    /// <param name="cancellationToken">Token to observe while waiting for the operation to complete.</param>
    Task MarkAsProcessingAsync(int notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a notification as played successfully.
    /// </summary>
    /// <param name="notificationId">The notification ID.</param>
    /// <param name="cancellationToken">Token to observe while waiting for the operation to complete.</param>
    Task MarkAsPlayedAsync(int notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a TTS attempt outcome for tracking purposes.
    /// </summary>
    /// <param name="notificationId">The notification ID.</param>
    /// <param name="provider">The TTS provider used (e.g., "AzureTTS", "EdgeTTS").</param>
    /// <param name="status">The outcome status ("success", "error", "cancelled", "skipped").</param>
    /// <param name="durationMs">Duration of the TTS operation in milliseconds.</param>
    /// <param name="cancellationToken">Token to observe while waiting for the operation to complete.</param>
    Task RecordTtsOutcomeAsync(int notificationId, string? provider, string status, int? durationMs, CancellationToken cancellationToken = default);
}
