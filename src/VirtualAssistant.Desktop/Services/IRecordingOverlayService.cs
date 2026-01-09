namespace Olbrasoft.VirtualAssistant.Desktop.Services;

/// <summary>
/// Service for displaying recording/transcribing overlay near cursor.
/// Phase 2 implementation - provides visual feedback during dictation.
/// </summary>
public interface IRecordingOverlayService : IAsyncDisposable
{
    /// <summary>
    /// Shows the recording overlay with blinking indicator.
    /// </summary>
    Task ShowRecordingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Shows the transcribing overlay with spinner.
    /// </summary>
    Task ShowTranscribingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Hides the overlay.
    /// </summary>
    Task HideAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the overlay position to near the current cursor.
    /// </summary>
    Task UpdatePositionAsync(CancellationToken cancellationToken = default);
}
