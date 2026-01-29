namespace Olbrasoft.VirtualAssistant.Data.Entities;

/// <summary>
/// Represents a failed LLM correction attempt.
/// </summary>
public class LlmError
{
    /// <summary>
    /// Unique identifier for this error.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to VoiceTranscription that failed to correct.
    /// </summary>
    public int VoiceTranscriptionId { get; set; }

    /// <summary>
    /// Error message describing why correction failed (NEVER NULL).
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// API call duration in milliseconds (how long before it failed).
    /// </summary>
    public int DurationMs { get; set; }

    /// <summary>
    /// When this error occurred (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to the original voice transcription.
    /// </summary>
    public VoiceTranscription VoiceTranscription { get; set; } = null!;
}
