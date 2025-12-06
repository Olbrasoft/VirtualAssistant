using Olbrasoft.Data.Entities.Abstractions;

namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Represents a voice transcription from Whisper.
/// </summary>
public class VoiceTranscription : BaseEnity
{
    /// <summary>
    /// Gets or sets the transcribed text from Whisper.
    /// </summary>
    public required string TranscribedText { get; set; }

    /// <summary>
    /// Gets or sets which application was focused when dictating (optional).
    /// </summary>
    public string? SourceApp { get; set; }

    /// <summary>
    /// Gets or sets the recording duration in milliseconds (optional).
    /// </summary>
    public int? DurationMs { get; set; }

    /// <summary>
    /// Gets or sets when the transcription was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
