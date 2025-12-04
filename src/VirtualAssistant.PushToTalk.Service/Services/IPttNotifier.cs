namespace Olbrasoft.VirtualAssistant.PushToTalk.Service.Services;

/// <summary>
/// Service interface for broadcasting Push-to-Talk events to connected clients.
/// </summary>
public interface IPttNotifier
{
    /// <summary>
    /// Notifies all connected clients that recording has started.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyRecordingStartedAsync();
    
    /// <summary>
    /// Notifies all connected clients that recording has stopped.
    /// </summary>
    /// <param name="durationSeconds">Duration of the recording in seconds.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyRecordingStoppedAsync(double durationSeconds);
    
    /// <summary>
    /// Notifies all connected clients that transcription has started.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyTranscriptionStartedAsync();
    
    /// <summary>
    /// Notifies all connected clients that transcription has completed successfully.
    /// </summary>
    /// <param name="text">The transcribed text.</param>
    /// <param name="confidence">Confidence score of the transcription.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyTranscriptionCompletedAsync(string text, float confidence);
    
    /// <summary>
    /// Notifies all connected clients that transcription has failed.
    /// </summary>
    /// <param name="errorMessage">The error message describing what went wrong.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyTranscriptionFailedAsync(string errorMessage);
    
    /// <summary>
    /// Notifies all connected clients about manual mute state change (ScrollLock).
    /// </summary>
    /// <param name="isMuted">True if muted (ScrollLock ON), false if unmuted.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyManualMuteChangedAsync(bool isMuted);
}
