namespace Olbrasoft.VirtualAssistant.Desktop.UI;

/// <summary>
/// Interface for recording overlay window abstraction.
/// Enables dependency injection and testing without GTK4 runtime.
/// </summary>
public interface IRecordingOverlayWindow : IDisposable
{
    /// <summary>
    /// Initializes the overlay window.
    /// Must be called before Show/Hide methods.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Shows the overlay with "Recording..." text at specified position.
    /// </summary>
    /// <param name="x">X coordinate (cursor position)</param>
    /// <param name="y">Y coordinate (cursor position)</param>
    void ShowRecording(int x, int y);

    /// <summary>
    /// Shows the overlay with "Transcribing..." text at specified position.
    /// </summary>
    /// <param name="x">X coordinate (cursor position)</param>
    /// <param name="y">Y coordinate (cursor position)</param>
    void ShowTranscribing(int x, int y);

    /// <summary>
    /// Hides the overlay.
    /// </summary>
    void Hide();

    /// <summary>
    /// Updates overlay position.
    /// </summary>
    /// <param name="cursorX">X coordinate (cursor position)</param>
    /// <param name="cursorY">Y coordinate (cursor position)</param>
    void UpdatePosition(int cursorX, int cursorY);
}
