namespace Olbrasoft.VirtualAssistant.PushToTalk;

/// <summary>
/// Interface for audio recording from microphone.
/// </summary>
public interface IAudioRecorder : IDisposable
{
    /// <summary>
    /// Event raised when audio data chunk is available.
    /// </summary>
    event EventHandler<AudioDataEventArgs>? AudioDataAvailable;

    /// <summary>
    /// Gets the sample rate in Hz.
    /// </summary>
    int SampleRate { get; }

    /// <summary>
    /// Gets the number of audio channels (1 for mono, 2 for stereo).
    /// </summary>
    int Channels { get; }

    /// <summary>
    /// Gets the bits per sample (typically 16).
    /// </summary>
    int BitsPerSample { get; }

    /// <summary>
    /// Gets a value indicating whether recording is currently active.
    /// </summary>
    bool IsRecording { get; }

    /// <summary>
    /// Starts recording audio from the microphone.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartRecordingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops recording audio from the microphone.
    /// </summary>
    Task StopRecordingAsync();

    /// <summary>
    /// Gets all recorded audio data as a byte array.
    /// </summary>
    byte[] GetRecordedData();
}
