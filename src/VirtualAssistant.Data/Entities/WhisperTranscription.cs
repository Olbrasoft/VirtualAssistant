using Olbrasoft.Data.Entities.Abstractions;

namespace Olbrasoft.VirtualAssistant.Data.Entities;

/// <summary>
/// Represents a Whisper AI transcription from voice input.
/// </summary>
public class WhisperTranscription : BaseEnity
{
    /// <summary>
    /// Gets or sets the transcribed text from Whisper AI.
    /// </summary>
    public required string TranscribedText { get; set; }

    /// <summary>
    /// Gets or sets the audio recording duration in milliseconds.
    /// </summary>
    public int? AudioDurationMs { get; set; }

    /// <summary>
    /// Gets or sets when the transcription was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
