namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Service for capturing audio from PipeWire using pw-record.
/// </summary>
public interface IAudioCaptureService : IDisposable
{
    /// <summary>
    /// Starts audio capture from PipeWire.
    /// Uses configured AudioSource if specified, otherwise default microphone.
    /// </summary>
    void Start();

    /// <summary>
    /// Reads one chunk of audio data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Audio chunk data, or null if end of stream.</returns>
    Task<byte[]?> ReadChunkAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops audio capture.
    /// </summary>
    void Stop();
}
