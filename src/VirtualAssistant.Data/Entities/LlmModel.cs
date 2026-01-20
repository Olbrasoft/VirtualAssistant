namespace Olbrasoft.VirtualAssistant.Data.Entities;

/// <summary>
/// Represents an LLM model used for ASR transcription correction.
/// Provider relationship is managed via ModelProviderMapping (many-to-many).
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
    /// Whether this model is currently active and available for use.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When this model was added to the system (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property - all corrections made by this model.
    /// </summary>
    public ICollection<LlmCorrection> LlmCorrections { get; set; } = new List<LlmCorrection>();

    /// <summary>
    /// Navigation property - providers that offer this model (many-to-many).
    /// </summary>
    public ICollection<ModelProviderMapping> ProviderMappings { get; set; } = [];

    /// <summary>
    /// Navigation property - notifications that used this LLM model.
    /// </summary>
    public ICollection<Notification> Notifications { get; set; } = [];
}
