namespace Olbrasoft.VirtualAssistant.Service.Workers.Streaming;

/// <summary>
/// Encapsulates the per-session streaming-chunk state that used to live
/// directly on <c>DictationWorker</c> (#969 god-class split). One instance
/// corresponds to one dictation session: the worker calls <see cref="Reset"/>
/// at StartRecording, forwards audio chunks through <see cref="SubmitChunk"/>,
/// and at Stop awaits <see cref="CombineAsync"/> for the aggregated text.
/// </summary>
public interface IStreamingChunkAssembler
{
    /// <summary>
    /// Whether streaming is active for the current session — mirrors the
    /// DictationWorker's frozen-at-start flag so stale chunks that arrive
    /// after a mode switch are silently ignored.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Clears all per-session state and installs a fresh CTS. If <paramref name="active"/>
    /// is false, future <see cref="SubmitChunk"/> calls are no-ops.
    /// </summary>
    void Reset(bool active);

    /// <summary>
    /// Cancels all in-flight chunk transcriptions and clears the aggregated
    /// results. Safe to call repeatedly; used on both Stop (slow mode that
    /// discards streaming results) and on dictation cancel.
    /// </summary>
    void CancelAndClear();

    /// <summary>
    /// Launches a background transcription for one recording chunk. No-op if
    /// streaming is not active for this session or the session has already
    /// been cancelled.
    /// </summary>
    void SubmitChunk(int index, byte[] pcmBytes);

    /// <summary>
    /// Awaits all in-flight chunk transcriptions and returns the combined
    /// text, or <c>null</c> if streaming is not active or produced no chunks.
    /// Honours <paramref name="cancellationToken"/> for the caller's wait; the
    /// underlying per-chunk tasks are driven by this assembler's own token.
    /// </summary>
    Task<string?> CombineAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Number of chunks whose transcription has completed (successful or
    /// empty). Exposed for the worker's post-combine logging.
    /// </summary>
    int CompletedChunkCount { get; }
}
