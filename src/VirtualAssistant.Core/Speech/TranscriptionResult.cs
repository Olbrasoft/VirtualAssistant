namespace Olbrasoft.VirtualAssistant.Core.Speech;

/// <summary>
/// Represents the result of speech transcription.
/// </summary>
public class TranscriptionResult
{
    /// <summary>
    /// Gets the transcribed text.
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
