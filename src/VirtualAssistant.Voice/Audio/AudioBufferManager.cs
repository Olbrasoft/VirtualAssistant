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

    public bool TryAppend(ReadOnlySpan<byte> chunk, int maxSizeBytes, out int newByteCount)
    {
        lock (_lock)
        {
            if (_buffer.Count + chunk.Length > maxSizeBytes)
            {
                newByteCount = _buffer.Count;
                return false;
            }

            // List<byte>.AddRange does not accept a span, so copy via a stackalloc-free
            // temp when the span is small or ToArray when large. The capture chunk sizes
            // here are on the order of 4-16 kB so ToArray is fine.
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
