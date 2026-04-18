using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Audio;

namespace Olbrasoft.VirtualAssistant.Voice.Audio;

/// <inheritdoc />
public sealed class ChunkEmissionScheduler : IChunkEmissionScheduler
{
    private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1);

    private readonly ILogger<ChunkEmissionScheduler> _logger;
    private readonly object _lock = new();

    private bool _enabled;
    private TimeSpan _interval = TimeSpan.FromSeconds(8);
    private int _cursor;
    private int _nextIndex;
    private DateTime _lastEmit;

    public ChunkEmissionScheduler(ILogger<ChunkEmissionScheduler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public event EventHandler<AudioChunkEventArgs>? ChunkAvailable;

    public void Enable(TimeSpan interval)
    {
        var normalized = interval < MinInterval ? MinInterval : interval;
        lock (_lock)
        {
            _interval = normalized;
            _enabled = true;
        }
        _logger.LogInformation("Chunk emission enabled with interval {Interval}s", normalized.TotalSeconds);
    }

    public void Disable()
    {
        lock (_lock) _enabled = false;
        _logger.LogInformation("Chunk emission disabled");
    }

    public void Reset()
    {
        lock (_lock)
        {
            _cursor = 0;
            _nextIndex = 0;
            _lastEmit = DateTime.UtcNow;
        }
    }

    public void TryEmitIfDue(IAudioBufferManager buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        byte[]? bytes = null;
        int chunkIndex = 0;

        lock (_lock)
        {
            if (!_enabled) return;
            if (DateTime.UtcNow - _lastEmit < _interval) return;

            var snapshot = buffer.CopyTail(_cursor, out var newCursor);
            if (snapshot.Length == 0) return;

            bytes = snapshot;
            _cursor = newCursor;
            chunkIndex = _nextIndex++;
            _lastEmit = DateTime.UtcNow;
        }

        FireChunkAvailable(chunkIndex, bytes, final: false);
    }

    public void EmitFinal(IAudioBufferManager buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        byte[]? bytes = null;
        int chunkIndex = 0;

        lock (_lock)
        {
            if (!_enabled) return;

            var snapshot = buffer.CopyTail(_cursor, out var newCursor);
            if (snapshot.Length == 0) return;

            bytes = snapshot;
            _cursor = newCursor;
            chunkIndex = _nextIndex++;
        }

        FireChunkAvailable(chunkIndex, bytes, final: true);
    }

    private void FireChunkAvailable(int chunkIndex, byte[] bytes, bool final)
    {
        try
        {
            ChunkAvailable?.Invoke(this, new AudioChunkEventArgs(chunkIndex, bytes));
            _logger.LogDebug("Emitted {Kind}audio chunk {Index}: {Bytes} bytes",
                final ? "final " : string.Empty, chunkIndex, bytes.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking ChunkAvailable handler for chunk {Index}", chunkIndex);
        }
    }
}
