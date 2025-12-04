namespace Olbrasoft.VirtualAssistant.Core.Speech;

/// <summary>
/// Interface for speech-to-text transcription.
/// </summary>
public interface ISpeechTranscriber : IDisposable
{
    /// <summary>
    /// Gets the language code for transcription (e.g., "cs" for Czech).
    /// </summary>
    string Language { get; }

    /// <summary>
    /// Transcribes audio data to text asynchronously.
    /// </summary>
    /// <param name="audioData">Audio data in WAV format.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transcription result with text and confidence.</returns>
    Task<TranscriptionResult> TranscribeAsync(byte[] audioData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transcribes audio stream to text asynchronously.
    /// </summary>
    /// <param name="audioStream">Audio stream in WAV format.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transcription result with text and confidence.</returns>
    Task<TranscriptionResult> TranscribeAsync(Stream audioStream, CancellationToken cancellationToken = default);
}
