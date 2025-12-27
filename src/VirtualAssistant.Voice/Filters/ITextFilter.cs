namespace Olbrasoft.VirtualAssistant.Voice.Filters;

/// <summary>
/// Applies text corrections to Whisper transcription output.
/// Supports both database-driven corrections and file-based filters.
/// </summary>
public interface ITextFilter
{
    /// <summary>
    /// Applies all filters to the input text.
    /// Order: Database corrections → File replacements → Remove patterns → Normalize
    /// </summary>
    /// <param name="text">Text to filter.</param>
    /// <returns>Filtered text with corrections and patterns applied.</returns>
    string Apply(string? text);

    /// <summary>
    /// Gets whether filtering is enabled (file-based or database).
    /// </summary>
    bool IsEnabled { get; }
}
