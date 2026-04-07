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

    /// <summary>
    /// List of strings that, when matching the entire transcription (after Trim, case-insensitive),
    /// cause the transcription to be wiped to empty. Use for short Whisper hallucinations
    /// (e.g. "Konec.", "Děkuji.") that should never be pasted alone but where the same word
    /// inside a longer legitimate sentence (e.g. "Konec konců...") must be preserved.
    /// </summary>
    public List<string> RemoveWholeText { get; set; } = new();

    /// <summary>
    /// List of regular expression patterns applied to the end of the transcription.
    /// Each pattern is anchored to end-of-text and matched case-insensitively. The matched
    /// suffix is removed but the prefix is preserved. Use for hallucinated suffixes such as
    /// "Titulky vytvořil JohnyX." appended to legitimate dictation.
    /// </summary>
    public List<string> RemoveSuffixRegex { get; set; } = new();
}
