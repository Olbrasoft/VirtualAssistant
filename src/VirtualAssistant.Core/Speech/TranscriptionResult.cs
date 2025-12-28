namespace Olbrasoft.VirtualAssistant.Core.Speech;

/// <summary>
/// Represents the result of speech transcription.
/// </summary>
public class TranscriptionResult
{
    /// <summary>
    /// Gets the transcribed text (final result after all processing).
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the confidence score (0.0 to 1.0).
    /// </summary>
    public float Confidence { get; }

    /// <summary>
    /// Gets a value indicating whether transcription was successful.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets the error message if transcription failed.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the original text from Whisper before any filtering or LLM correction.
    /// Null if no processing was applied or if transcription failed.
    /// </summary>
    public string? OriginalText { get; init; }

    /// <summary>
    /// Gets the text after filtering but before LLM correction.
    /// Null if text filter was not applied or if transcription failed.
    /// </summary>
    public string? FilteredText { get; init; }

    /// <summary>
    /// Initializes a new instance for successful transcription.
    /// </summary>
    public TranscriptionResult(string text, float confidence)
    {
        Text = text;
        Confidence = confidence;
        Success = true;
        ErrorMessage = null;
    }

    /// <summary>
    /// Initializes a new instance for failed transcription.
    /// </summary>
    public TranscriptionResult(string errorMessage)
    {
        Text = string.Empty;
        Confidence = 0.0f;
        Success = false;
        ErrorMessage = errorMessage;
    }
}
