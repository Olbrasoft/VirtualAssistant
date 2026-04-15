namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Interface for controlling dictation functionality.
/// </summary>
public interface IDictationControl
{
    /// <summary>
    /// Enables or disables dictation functionality.
    /// When disabled, CapsLock key events are ignored.
    /// If currently recording/transcribing, performs emergency stop.
    /// </summary>
    /// <param name="enabled">True to enable dictation, false to disable.</param>
    void SetDictationEnabled(bool enabled);

    /// <summary>
    /// Enables or disables streaming (chunked) transcription for the fast dictation path.
    /// When enabled, audio is sliced and transcribed in background during recording.
    /// Affects only fast-mode dictations started after the toggle; in-flight recordings retain their mode.
    /// </summary>
    void SetStreamingTranscriptionEnabled(bool enabled);
}
