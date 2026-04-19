using Olbrasoft.VirtualAssistant.Core.Speech;

namespace Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

/// <summary>
/// Per-session transcription pipeline lifted out of <c>DictationWorker</c>
/// (#969 god-class split). Bundles the stream-chunk state machine
/// (<c>IStreamingChunkAssembler</c>) and the transcription service
/// (<c>ITranscriptionService</c>) behind one abstraction so the worker
/// only handles orchestration/UX and never touches the two backends
/// directly. The raw transcription method is streaming-aware — when
/// <see cref="BeginSession"/> was called with <c>streamingActive=true</c>
/// and chunks were forwarded via <see cref="ForwardChunk"/>, the combined
/// streaming result is used instead of running Whisper on the full buffer.
/// </summary>
public interface IDictationTranscriber
{
    /// <summary>
    /// Raised when a raw transcription (pre-LLM-correction) becomes available.
    /// Forwarded from the underlying <c>ITranscriptionService</c> so the worker
    /// can broadcast without having to know which transcription backend is
    /// wired up. Raised once per transcription before any LLM correction.
    /// Handlers should be fast and non-blocking; no specific thread affinity
    /// is guaranteed.
    /// </summary>
    event Action<string>? RawTranscriptionReady;

    /// <summary>
    /// Whether a streaming session is currently active (<see cref="BeginSession"/>
    /// was called with <c>streamingActive=true</c> and <see cref="EndSession"/>
    /// hasn't run yet). Exposed so the worker can skip streaming-specific
    /// cleanup when slow-mode overrides the start-time choice.
    /// </summary>
    bool IsStreamingActive { get; }

    /// <summary>
    /// Start a new transcription session; resets the streaming-chunk state.
    /// Pass <c>streamingActive=true</c> only when the worker will forward
    /// <see cref="ForwardChunk"/> calls during recording.
    /// </summary>
    void BeginSession(bool streamingActive);

    /// <summary>
    /// Forwards an audio chunk from the recording coordinator to the
    /// streaming assembler. Ignored if streaming is not active for the
    /// current session.
    /// </summary>
    void ForwardChunk(int index, byte[] pcmBytes);

    /// <summary>
    /// Ends the current transcription session; cancels in-flight streaming
    /// tasks and clears the chunk state. Safe to call repeatedly.
    /// </summary>
    void EndSession();

    /// <summary>
    /// Raw (no-LLM-correction) transcription. When the session was started
    /// with streaming and chunks were forwarded, combines the cached
    /// per-chunk transcriptions; otherwise runs the raw Whisper pass on
    /// the full buffer. Falls back to the full-buffer pass if the
    /// streaming combine produced empty text.
    /// </summary>
    Task<TranscriptionResult> TranscribeRawAsync(byte[] audio, CancellationToken cancellationToken);

    /// <summary>
    /// Full transcription with LLM correction, filters, and racing as
    /// configured on <c>ITranscriptionService</c>. Not streaming-aware —
    /// this path runs on the full buffer regardless of session state.
    /// </summary>
    Task<TranscriptionResult> TranscribeFullAsync(byte[] audio, CancellationToken cancellationToken);
}
