using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Manages audio buffers for speech recording.
/// </summary>
public class SpeechBufferManager : ISpeechBufferManager
{
    private readonly ILogger<SpeechBufferManager> _logger;
    private readonly int _preBufferMaxBytes;

    private readonly Queue<byte[]> _preBuffer = new();
    private readonly List<byte[]> _speechBuffer = new();
    private int _preBufferBytes;
    private int _speechBufferBytes;

    /// <inheritdoc />
    public int SpeechBufferSize => _speechBufferBytes;

    public SpeechBufferManager(
        ILogger<SpeechBufferManager> logger,
        IOptions<ContinuousListenerOptions> options)
    {
        _logger = logger;
        _preBufferMaxBytes = options.Value.PreBufferMaxBytes;
    }

    /// <inheritdoc />
    public void AddToPreBuffer(byte[] chunk)
    {
        _preBuffer.Enqueue(chunk);
        _preBufferBytes += chunk.Length;

        // Trim if too large
        while (_preBufferBytes > _preBufferMaxBytes && _preBuffer.Count > 0)
        {
            var removed = _preBuffer.Dequeue();
            _preBufferBytes -= removed.Length;
        }
    }

    /// <inheritdoc />
    public void TransferPreBufferToSpeech()
    {
        while (_preBuffer.Count > 0)
        {
            var chunk = _preBuffer.Dequeue();
            _speechBuffer.Add(chunk);
            _speechBufferBytes += chunk.Length;
        }
        _preBufferBytes = 0;

        _logger.LogDebug("Transferred pre-buffer to speech buffer. Total size: {Size} bytes", _speechBufferBytes);
    }

    /// <inheritdoc />
    public void AddToSpeechBuffer(byte[] chunk)
    {
        _speechBuffer.Add(chunk);
        _speechBufferBytes += chunk.Length;
    }

    /// <inheritdoc />
    public byte[] GetCombinedSpeechData()
    {
        var audioData = new byte[_speechBufferBytes];
        int offset = 0;

        foreach (var chunk in _speechBuffer)
        {
            Buffer.BlockCopy(chunk, 0, audioData, offset, chunk.Length);
            offset += chunk.Length;
        }

        _logger.LogDebug("Combined speech data: {Size} bytes", audioData.Length);
        return audioData;
    }

    /// <inheritdoc />
    public void ClearSpeechBuffer()
    {
        _speechBuffer.Clear();
        _speechBufferBytes = 0;
    }

    /// <inheritdoc />
    public void ClearAll()
    {
        _preBuffer.Clear();
        _preBufferBytes = 0;
        _speechBuffer.Clear();
        _speechBufferBytes = 0;

        _logger.LogDebug("All buffers cleared");
    }
}
