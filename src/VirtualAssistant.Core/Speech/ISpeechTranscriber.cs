namespace Olbrasoft.VirtualAssistant.Core.Speech;

/// <summary>
/// Interface for speech-to-text transcription.
/// </summary>
public interface ISpeechTranscriber : IDisposable
{
    /// <summary>
    /// Gets the provider key used in configuration (lowercase, e.g. "whisper", "google").
    /// Acts as the identity by which the factory maps a requested provider name to an instance,
    /// without any switch statement coupled to the set of available providers.
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    /// Gets the provider name as stored in the <c>providers</c> database table (e.g.,
    /// "Whisper Local", "Google Speech-to-Text"). Used only for tracking — database
    /// rows stay human-readable while the factory lookup stays key-driven.
    /// </summary>
    string DatabaseName { get; }

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
