namespace Olbrasoft.VirtualAssistant.Data.Entities;

/// <summary>
/// Represents an LLM correction attempt for a voice transcription.
/// </summary>
public class LlmCorrection
{
    /// <summary>
    /// Unique identifier for this correction.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to VoiceTranscription (original text is stored there - normalization).
    /// </summary>
    public int VoiceTranscriptionId { get; set; }

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
    /// NULL indicates no prompt tracking (e.g., legacy code or system without desktop monitoring).
    /// </summary>
    public int? PromptId { get; set; }

    /// <summary>
    /// Foreign key to LlmModel - which model performed this correction.
    /// Required - every correction must be associated with a model.
    /// </summary>
    public int ModelId { get; set; }

    /// <summary>
    /// Navigation property to the original voice transcription.
    /// </summary>
    public VoiceTranscription VoiceTranscription { get; set; } = null!;

    /// <summary>
    /// Navigation property to the prompt that was used for this correction.
    /// </summary>
    public Prompt Prompt { get; set; } = null!;

    /// <summary>
    /// Navigation property to the model that performed this correction.
    /// </summary>
    public LlmModel Model { get; set; } = null!;
}
