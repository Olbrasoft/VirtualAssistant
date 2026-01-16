namespace Olbrasoft.VirtualAssistant.Data.Entities;

/// <summary>
/// Represents an LLM model used for ASR transcription correction.
/// Links to Provider for external service tracking.
/// </summary>
public class LlmModel
{
    /// <summary>
    /// Unique identifier for this model.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Display name for UI (e.g., "Mistral Large", "Alpha GLM 4.7").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Model identifier used in API calls (e.g., "mistral-large-latest", "alpha-glm-4.7").
    /// </summary>
    public required string ModelIdentifier { get; set; }

    /// <summary>
    /// Foreign key to Provider.
    /// </summary>
    public int ProviderId { get; set; }

    /// <summary>
    /// Whether this model is currently active and available for use.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When this model was added to the system (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to the provider.
    /// </summary>
    public Provider Provider { get; set; } = null!;

    /// <summary>
    /// Navigation property - all corrections made by this model.
    /// </summary>
    public ICollection<LlmCorrection> LlmCorrections { get; set; } = new List<LlmCorrection>();
}
