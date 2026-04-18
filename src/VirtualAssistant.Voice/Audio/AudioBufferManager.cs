namespace Olbrasoft.VirtualAssistant.Voice.Audio;

/// <inheritdoc />
public sealed class AudioBufferManager : IAudioBufferManager
{
    private readonly List<byte> _buffer = new();
    private readonly object _lock = new();

    public int ByteCount
    {
        get
        {
            lock (_lock) return _buffer.Count;
        }
    }

    /// <summary>
    /// Array overload — preferred on the capture hot path because it skips the
    /// extra copy that the span overload has to do to feed
    /// <see cref="List{T}.AddRange(IEnumerable{T})"/>.
    /// </summary>
    public bool TryAppend(byte[] chunk, int maxSizeBytes, out int newByteCount)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        lock (_lock)
        {
            if (_buffer.Count + chunk.Length > maxSizeBytes)
            {
                newByteCount = _buffer.Count;
                return false;
            }

            _buffer.AddRange(chunk);
            newByteCount = _buffer.Count;
            return true;
        }
    }

    public bool TryAppend(ReadOnlySpan<byte> chunk, int maxSizeBytes, out int newByteCount)
    {
        lock (_lock)
        {
            if (_buffer.Count + chunk.Length > maxSizeBytes)
            {
                newByteCount = _buffer.Count;
                return false;
            }

            // List<byte>.AddRange has no span overload, so the ReadOnlySpan path
            // pays one extra copy via ToArray. Callers that already hold a byte[]
            // (the capture loop, in practice) should prefer the array overload
            // above to avoid this clone. Capture chunks are ~4-16 kB so the
            // clone is acceptable, but not free.
            _buffer.AddRange(chunk.ToArray());
            newByteCount = _buffer.Count;
            return true;
        }
    }

    public void Clear()
    {
        lock (_lock) _buffer.Clear();
    }

    public byte[] CopyTail(int fromCursor, out int newEndCursor)
    {
        lock (_lock)
        {
            var end = _buffer.Count;
            if (fromCursor >= end)
            {
                newEndCursor = end;
                return Array.Empty<byte>();
            }

            var length = end - fromCursor;
            var result = new byte[length];
            _buffer.CopyTo(fromCursor, result, 0, length);
            newEndCursor = end;
            return result;
        }
    }

    public byte[] DrainToArray()
    {
        lock (_lock)
        {
            var result = _buffer.ToArray();
            _buffer.Clear();
            return result;
        }
    }
}
