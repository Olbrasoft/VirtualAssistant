using Olbrasoft.VirtualAssistant.Core.Speech;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Service for transcribing audio with optional LLM correction.
/// Pipeline: STT → Text Filtering → LLM correction.
/// </summary>
public interface ITranscriptionService : IDisposable
{
    /// <summary>
    /// Raised when raw STT transcription is ready, before LLM correction.
    /// </summary>
    event Action<string>? RawTranscriptionReady;

    /// <summary>
    /// Initializes the transcription service.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Transcribes audio data through the STT → filtering → LLM pipeline.
    /// If audio is too large, it will be truncated to meet service limits.
    /// </summary>
    /// <param name="audioData">16-bit PCM audio data at 16kHz.</param>
    /// <param name="cancellationToken">Cancellation token to abort transcription.</param>
    /// <returns>Transcription result.</returns>
    Task<TranscriptionResult> TranscribeAsync(byte[] audioData, CancellationToken cancellationToken = default);
}
