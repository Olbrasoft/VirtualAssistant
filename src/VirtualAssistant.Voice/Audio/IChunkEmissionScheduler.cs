using Olbrasoft.VirtualAssistant.Core.Audio;

namespace Olbrasoft.VirtualAssistant.Voice.Audio;

/// <summary>
/// Owns the chunk-emission timing state that used to live inline in
/// <see cref="AudioRecordingCoordinator"/> (enabled flag, interval, cursor,
/// ordinal, last-emit timestamp) and fires <see cref="ChunkAvailable"/>
/// when a new slice of the audio buffer is ready to be handed off.
/// </summary>
public interface IChunkEmissionScheduler
{
    /// <summary>
    /// Raised outside the scheduler's lock so subscribers can re-enter without
    /// deadlocking. A subscriber exception is caught and logged; it never
    /// propagates into the capture loop.
    /// </summary>
    event EventHandler<AudioChunkEventArgs>? ChunkAvailable;

    /// <summary>
    /// Enables emission with the given minimum interval between chunks.
    /// Intervals under 1 s are clamped to 1 s (matches legacy behavior).
    /// </summary>
    void Enable(TimeSpan interval);

    /// <summary>
    /// Disables emission. Previously emitted chunks remain with their subscribers.
    /// </summary>
    void Disable();

    /// <summary>
    /// Resets cursor, ordinal, and last-emit timestamp. Called at the start of
    /// a new recording session.
    /// </summary>
    void Reset();

    /// <summary>
    /// Emits a chunk if emission is enabled, the interval has elapsed, and there
    /// is data past the cursor. Called after every capture-chunk append.
    /// </summary>
    void TryEmitIfDue(IAudioBufferManager buffer);

    /// <summary>
    /// Emits the tail as a final chunk regardless of the interval, provided
    /// emission is enabled and there is pending data. Called once from the
    /// stop path before the coordinator drains the buffer.
    /// </summary>
    void EmitFinal(IAudioBufferManager buffer);
}
