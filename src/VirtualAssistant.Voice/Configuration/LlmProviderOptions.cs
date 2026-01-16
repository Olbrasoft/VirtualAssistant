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

    private string _activeProvider = "mistral";

    /// <summary>
    /// Gets or sets the active provider name (e.g., "mistral", "zen").
    /// Never returns null; falls back to the default value when null or whitespace is configured.
    /// </summary>
    public string ActiveProvider
    {
        get => string.IsNullOrWhiteSpace(_activeProvider) ? "mistral" : _activeProvider;
        set => _activeProvider = string.IsNullOrWhiteSpace(value) ? "mistral" : value;
    }
}
