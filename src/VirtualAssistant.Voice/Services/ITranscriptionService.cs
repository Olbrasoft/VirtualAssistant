using Olbrasoft.VirtualAssistant.Core.Speech;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Interface for transcribing audio using speech-to-text service.
/// </summary>
public interface ITranscriptionService : IDisposable
{
    /// <summary>
    /// Initializes transcriber.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Transcribes audio data.
    /// </summary>
    /// <param name="audioData">16-bit PCM audio data at 16kHz.</param>
    /// <param name="cancellationToken">Cancellation token to abort transcription.</param>
    /// <returns>Transcription result.</returns>
    Task<TranscriptionResult> TranscribeAsync(byte[] audioData, CancellationToken cancellationToken = default);
}
