namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Tracks when and how transcription corrections are used.
/// Allows measuring correction effectiveness and usage patterns.
/// </summary>
public class TranscriptionCorrectionUsage
{
    /// <summary>
    /// Unique identifier for this usage record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the correction that was used.
    /// </summary>
    public int CorrectionId { get; set; }

    /// <summary>
    /// Navigation property to the correction.
    /// </summary>
    public TranscriptionCorrection Correction { get; set; } = null!;

    /// <summary>
    /// When this correction was used.
    /// </summary>
    public DateTimeOffset UsedAt { get; set; }

    /// <summary>
    /// Optional context about where/how the correction was used.
    /// Example: "dictation", "continuous-listening", "push-to-talk"
    /// </summary>
    public string? Context { get; set; }
}
