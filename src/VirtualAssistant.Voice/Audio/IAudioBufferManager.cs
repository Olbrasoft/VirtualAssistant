namespace Olbrasoft.VirtualAssistant.Voice.Audio;

/// <summary>
/// Thread-safe buffer for PCM bytes captured during a recording session.
/// Owns the write-lock previously held by <see cref="AudioRecordingCoordinator"/>
/// so that buffer state is encapsulated behind one class instead of spread
/// across lock-protected sections on the coordinator.
/// </summary>
public interface IAudioBufferManager
{
    /// <summary>
    /// Current number of buffered bytes. Safe to read concurrently.
    /// </summary>
    int ByteCount { get; }

    /// <summary>
    /// Appends <paramref name="chunk"/> iff doing so would not exceed
    /// <paramref name="maxSizeBytes"/>. Returns <c>false</c> (without mutation)
    /// when the limit would be breached so the caller can stop the capture
    /// loop cleanly.
    /// </summary>
    bool TryAppend(ReadOnlySpan<byte> chunk, int maxSizeBytes, out int newByteCount);

    /// <summary>
    /// Resets the buffer to empty. Used at the start of a new recording session.
    /// </summary>
    void Clear();

    /// <summary>
    /// Returns a copy of the buffer from <paramref name="fromCursor"/> to the
    /// current end. <paramref name="newEndCursor"/> is set to the end index so
    /// the caller can advance its own cursor. The snapshot is taken atomically
    /// under the internal lock.
    /// </summary>
    byte[] CopyTail(int fromCursor, out int newEndCursor);

    /// <summary>
    /// Atomically copies the whole buffer into a new array and clears it.
    /// Used at the end of a recording session to drain the result.
    /// </summary>
    byte[] DrainToArray();
}
