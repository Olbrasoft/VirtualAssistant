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

    /// <summary>
    /// Transcribes audio data using STT only (no text filtering, no LLM correction).
    /// Used for quick dictation mode where speed is prioritized over accuracy.
    /// </summary>
    /// <param name="audioData">16-bit PCM audio data at 16kHz.</param>
    /// <param name="cancellationToken">Cancellation token to abort transcription.</param>
    /// <returns>Transcription result with raw STT text.</returns>
    Task<TranscriptionResult> TranscribeRawAsync(byte[] audioData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finalizes a pre-transcribed raw text (produced externally, e.g. by combining
    /// streaming chunk transcriptions) by emitting RawTranscriptionReady and applying
    /// the lightweight filter, without running Whisper again.
    /// </summary>
    /// <param name="rawText">Concatenated raw STT text from chunk transcriptions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Filtered transcription result equivalent to the tail of TranscribeRawAsync.</returns>
    Task<TranscriptionResult> FinalizePreTranscribedRawAsync(string rawText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs Whisper on a single audio chunk and returns the raw text, bypassing
    /// events and filters. Used by the streaming transcription path in DictationWorker.
    /// </summary>
    /// <param name="audioData">16-bit PCM audio at 16kHz.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Raw Whisper output for this chunk; empty string on failure.</returns>
    Task<string> TranscribeChunkRawAsync(byte[] audioData, CancellationToken cancellationToken = default);
}
