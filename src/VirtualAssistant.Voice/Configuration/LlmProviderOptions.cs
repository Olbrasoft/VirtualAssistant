namespace Olbrasoft.VirtualAssistant.Voice.Configuration;

/// <summary>
/// Configuration options for LLM provider selection.
/// </summary>
public class LlmProviderOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "LlmProvider";

    /// <summary>
    /// Gets or sets the active provider name (e.g., "mistral", "zen").
    /// </summary>
    public string ActiveProvider { get; set; } = "mistral";
}
