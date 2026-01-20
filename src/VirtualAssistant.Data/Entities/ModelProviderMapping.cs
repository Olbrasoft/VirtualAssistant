namespace Olbrasoft.VirtualAssistant.Data.Entities;

/// <summary>
/// Many-to-many relationship between LLM models and providers.
/// One model can be available through multiple providers (e.g., Claude via Anthropic and Antigravity proxy).
/// </summary>
public class ModelProviderMapping
{
    /// <summary>
    /// Foreign key to LlmModel.
    /// </summary>
    public int ModelId { get; set; }

    /// <summary>
    /// Navigation property to the LLM model.
    /// </summary>
    public LlmModel Model { get; set; } = null!;

    /// <summary>
    /// Foreign key to Provider.
    /// </summary>
    public int ProviderId { get; set; }

    /// <summary>
    /// Navigation property to the provider.
    /// </summary>
    public Provider Provider { get; set; } = null!;

    /// <summary>
    /// When this mapping was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
