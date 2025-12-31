namespace Olbrasoft.VirtualAssistant.Voice.SpeechToText;

/// <summary>
/// Defines a speech-to-text provider that can transcribe audio to text.
/// </summary>
public interface ITranscriptionProvider
{
    /// <summary>
    /// Gets the unique name identifying this provider (e.g., "WhisperNet", "Azure", "Google").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Transcribes speech from the given audio data.
    /// </summary>
    /// <param name="request">The transcription request containing audio data and settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transcription result containing text or error information.</returns>
    Task<SttTranscriptionResult> TranscribeAsync(TranscriptionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about this provider including status and supported languages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider information.</returns>
    Task<TranscriptionProviderInfo> GetInfoAsync(CancellationToken cancellationToken = default);
}
