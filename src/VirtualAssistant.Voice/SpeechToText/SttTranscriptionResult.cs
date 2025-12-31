namespace Olbrasoft.VirtualAssistant.Voice.SpeechToText;

/// <summary>
/// Represents the result of a speech-to-text transcription operation from SpeechToText provider.
/// Renamed to avoid conflict with VirtualAssistant.Core.Speech.TranscriptionResult.
/// </summary>
public sealed class SttTranscriptionResult
{
    /// <summary>
    /// Gets whether the transcription was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the error message if transcription failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the transcribed text (null if transcription failed).
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Gets the detected or specified language code.
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Gets the name of the provider that produced this result.
    /// </summary>
    public string? ProviderUsed { get; init; }

    /// <summary>
    /// Gets the duration of the audio content.
    /// </summary>
    public TimeSpan? AudioDuration { get; init; }

    /// <summary>
    /// Gets the time taken to transcribe the audio (latency).
    /// </summary>
    public TimeSpan TranscriptionTime { get; init; }

    /// <summary>
    /// Gets the confidence score (0.0 to 1.0, if supported by provider).
    /// </summary>
    public float? Confidence { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static SttTranscriptionResult Ok(
        string text,
        string providerUsed,
        TimeSpan transcriptionTime,
        string? language = null,
        TimeSpan? audioDuration = null,
        float? confidence = null)
        => new()
        {
            Success = true,
            Text = text,
            ProviderUsed = providerUsed,
            TranscriptionTime = transcriptionTime,
            Language = language,
            AudioDuration = audioDuration,
            Confidence = confidence
        };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static SttTranscriptionResult Fail(
        string errorMessage,
        string? providerUsed = null,
        TimeSpan transcriptionTime = default)
        => new()
        {
            Success = false,
            ErrorMessage = errorMessage,
            ProviderUsed = providerUsed,
            TranscriptionTime = transcriptionTime
        };
}
