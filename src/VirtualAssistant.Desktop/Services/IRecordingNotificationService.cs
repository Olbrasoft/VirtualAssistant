namespace Olbrasoft.VirtualAssistant.Desktop.Services;

/// <summary>
/// Service for showing recording/transcribing status notifications.
/// Uses D-Bus org.freedesktop.Notifications for desktop notifications.
/// </summary>
public interface IRecordingNotificationService : IAsyncDisposable
{
    /// <summary>
    /// Shows a notification indicating that recording is in progress.
    /// </summary>
    Task ShowRecordingAsync(CancellationToken ct = default);

    /// <summary>
    /// Shows a notification indicating that transcription is in progress.
    /// </summary>
    Task ShowTranscribingAsync(CancellationToken ct = default);

    /// <summary>
    /// Hides any active recording/transcribing notification.
    /// </summary>
    Task HideAsync(CancellationToken ct = default);
}
