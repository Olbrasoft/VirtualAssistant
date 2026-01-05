namespace Olbrasoft.VirtualAssistant.Data.Entities;

/// <summary>
/// Represents an LLM correction attempt for a Whisper transcription.
/// </summary>
public class LlmCorrection
{
    /// <summary>
    /// Unique identifier for this correction.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to WhisperTranscription (original text is stored there - normalization).
    /// </summary>
    public int WhisperTranscriptionId { get; set; }

    /// <summary>
    /// Corrected text returned by LLM (NEVER NULL - successful corrections only).
    /// </summary>
    public string CorrectedText { get; set; } = string.Empty;

    /// <summary>
    /// API call duration in milliseconds.
    /// </summary>
    public int DurationMs { get; set; }

    /// <summary>
    /// When this correction was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Foreign key to Prompt - which context-aware prompt was used for this correction.
    /// NOT NULL - every correction always has a prompt (specific or Default with pattern "*").
    /// </summary>
    public int PromptId { get; set; }

    /// <summary>
    /// Navigation property to the original Whisper transcription.
    /// </summary>
    public WhisperTranscription WhisperTranscription { get; set; } = null!;

    /// <summary>
    /// Navigation property to the prompt that was used for this correction.
    /// </summary>
    public Prompt Prompt { get; set; } = null!;
}
