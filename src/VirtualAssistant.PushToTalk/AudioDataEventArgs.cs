namespace Olbrasoft.VirtualAssistant.PushToTalk;

/// <summary>
/// Event arguments for audio data availability.
/// </summary>
public class AudioDataEventArgs : EventArgs
{
    /// <summary>
    /// Gets the audio data buffer.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Gets the timestamp when the audio was captured.
    /// </summary>
    public DateTime Timestamp { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioDataEventArgs"/> class.
    /// </summary>
    /// <param name="data">Audio data buffer.</param>
    /// <param name="timestamp">Capture timestamp.</param>
    public AudioDataEventArgs(byte[] data, DateTime timestamp)
    {
        Data = data;
        Timestamp = timestamp;
    }
}
