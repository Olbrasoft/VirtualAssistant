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

    /// <summary>
    /// Racing configuration for parallel LLM provider calls.
    /// </summary>
    public RacingOptions Racing { get; set; } = new();
}

/// <summary>
/// Configuration for LLM racing pattern - calling multiple providers in parallel
/// and using whichever responds first.
/// </summary>
public class RacingOptions
{
    /// <summary>
    /// Enable or disable racing mode. When enabled, multiple providers are called
    /// simultaneously and the first successful response is used.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// List of provider names to race (e.g., ["mercury", "zen"]).
    /// Must contain at least 2 providers when racing is enabled.
    /// </summary>
    public List<string> Providers { get; set; } = [];
}
