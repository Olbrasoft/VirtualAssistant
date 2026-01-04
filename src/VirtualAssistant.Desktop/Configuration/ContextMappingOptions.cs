namespace Olbrasoft.VirtualAssistant.Desktop.Configuration;

/// <summary>
/// Configuration for mapping applications to context types.
/// </summary>
public class ContextMappingOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "ContextMapping";

    /// <summary>
    /// Application IDs for programming context (IDEs, code editors).
    /// </summary>
    public string[] Programming { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Application IDs for chat context (messaging apps).
    /// </summary>
    public string[] Chat { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Application IDs for browsing context (web browsers).
    /// </summary>
    public string[] Browsing { get; set; } = Array.Empty<string>();
}
