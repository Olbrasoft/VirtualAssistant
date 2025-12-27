namespace Olbrasoft.VirtualAssistant.Voice.Filters;

/// <summary>
/// Configuration for text filters loaded from JSON file.
/// </summary>
public class TextFiltersConfig
{
    /// <summary>
    /// List of text patterns to remove from transcription output.
    /// </summary>
    public List<string> Remove { get; set; } = new();

    /// <summary>
    /// Dictionary of text replacements (incorrect -> correct).
    /// File-based fallback if database is not available.
    /// </summary>
    public Dictionary<string, string> Replace { get; set; } = new();

    /// <summary>
    /// Whether to enable database-driven corrections.
    /// Default is true.
    /// </summary>
    public bool EnableDatabaseCorrections { get; set; } = true;
}
