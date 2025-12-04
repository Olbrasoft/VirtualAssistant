namespace Olbrasoft.VirtualAssistant.PushToTalk.Service.Models;

/// <summary>
/// Types of Push-to-Talk events that can be broadcast to clients.
/// </summary>
public enum PttEventType
{
    /// <summary>Recording has started.</summary>
    RecordingStarted,
    
    /// <summary>Recording has stopped.</summary>
    RecordingStopped,
    
    /// <summary>Transcription process has started.</summary>
    TranscriptionStarted,
    
    /// <summary>Transcription completed successfully.</summary>
    TranscriptionCompleted,
    
    /// <summary>Transcription failed.</summary>
    TranscriptionFailed,
    
    /// <summary>Manual mute activated (ScrollLock ON).</summary>
    ManualMuteOn,
    
    /// <summary>Manual mute deactivated (ScrollLock OFF).</summary>
    ManualMuteOff
}

/// <summary>
/// Data transfer object for Push-to-Talk dictation events.
/// </summary>
public record PttEvent
{
    /// <summary>
    /// Gets the type of the event.
    /// </summary>
    public PttEventType EventType { get; init; }
    
    /// <summary>
    /// Gets the UTC timestamp when the event occurred.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets the transcribed text (only for TranscriptionCompleted events).
    /// </summary>
    public string? Text { get; init; }
    
    /// <summary>
    /// Gets the confidence score of transcription (0.0 to 1.0).
    /// </summary>
    public float? Confidence { get; init; }
    
    /// <summary>
    /// Gets the duration of the recording in seconds.
    /// </summary>
    public double? DurationSeconds { get; init; }
    
    /// <summary>
    /// Gets the error message (only for TranscriptionFailed events).
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// Gets the version of the service.
    /// </summary>
    public string ServiceVersion { get; init; } = "1.0.0";
}
