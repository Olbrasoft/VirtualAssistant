using Olbrasoft.Data.Entities.Abstractions;

namespace Olbrasoft.VirtualAssistant.Data.Entities;

/// <summary>
/// Represents a voice transcription from speech-to-text provider.
/// </summary>
public class VoiceTranscription : BaseEnity
{
    /// <summary>
    /// Gets or sets the transcribed text from STT provider.
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

    /// <summary>
    /// Gets or sets the ID of the STT provider that created this transcription.
    /// </summary>
    public int ProviderId { get; set; }

    /// <summary>
    /// Navigation property to the STT provider.
    /// </summary>
    public Provider Provider { get; set; } = null!;
}
