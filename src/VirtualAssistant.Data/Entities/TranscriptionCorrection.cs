namespace Olbrasoft.VirtualAssistant.Data.Entities;

/// <summary>
/// Represents a correction mapping for ASR transcription errors.
/// Used to automatically fix recurring Whisper transcription mistakes.
/// </summary>
public class TranscriptionCorrection
{
    /// <summary>
    /// Unique identifier for this correction.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The incorrect text as transcribed by Whisper ASR.
    /// Example: "vyspru", "kapslok", "teďkon"
    /// </summary>
    public string IncorrectText { get; set; } = string.Empty;

    /// <summary>
    /// The correct text that should replace the incorrect version.
    /// Example: "Whisper", "Caps Lock", "teď"
    /// </summary>
    public string CorrectText { get; set; } = string.Empty;

    /// <summary>
    /// Whether the correction should be case-sensitive.
    /// Default is false for flexibility.
    /// </summary>
    public bool CaseSensitive { get; set; }

    /// <summary>
    /// Priority for applying corrections (higher = applied first).
    /// Useful for preventing partial matches from interfering.
    /// Example: "teďkon" (priority 100) should be applied before "teď" (priority 50)
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Whether this correction is active and should be applied.
    /// Allows temporarily disabling corrections without deleting them.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional notes explaining why this correction exists.
    /// Example: "Common speech concatenation error", "ASR engine name"
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// When this correction was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// When this correction was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
