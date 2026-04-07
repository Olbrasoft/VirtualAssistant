namespace Olbrasoft.VirtualAssistant.Voice.Filters;

/// <summary>
/// A reduced text filter pipeline used in latency-sensitive paths such as Quick Dictation,
/// where the full <see cref="ITextFilter"/> pipeline (database corrections, file replacements,
/// LLM correction) would add unacceptable latency. Implementations apply only the cheapest
/// strategies that have no I/O cost (regex, string operations) and that catch the most
/// common Whisper artifacts before the text is pasted into the target application.
/// </summary>
public interface ILightweightTextFilter
{
    /// <summary>
    /// Applies the lightweight filter pipeline to the input text.
    /// </summary>
    /// <param name="text">Text to filter.</param>
    /// <returns>Filtered text. May be empty if the entire transcription was a hallucination.</returns>
    string Apply(string? text);

    /// <summary>
    /// Gets whether at least one underlying strategy is currently enabled.
    /// </summary>
    bool IsEnabled { get; }
}
