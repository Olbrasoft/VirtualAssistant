namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Interface for Voice Activity Detection service.
/// </summary>
public interface IVadService : IDisposable
{
    /// <summary>
    /// Detects if the audio chunk contains speech.
    /// </summary>
    /// <param name="pcmData">16-bit PCM audio data at 16kHz.</param>
    /// <returns>True if speech detected, false otherwise.</returns>
    bool IsSpeech(byte[] pcmData);

    /// <summary>
    /// Detects if the audio chunk contains speech and returns the probability.
    /// </summary>
    /// <param name="pcmData">16-bit PCM audio data at 16kHz.</param>
    /// <returns>Tuple of (isSpeech, probability 0.0-1.0).</returns>
    (bool IsSpeech, float Probability) Analyze(byte[] pcmData);

    /// <summary>
    /// Resets the internal state of the VAD model.
    /// Call this when starting a new recording session.
    /// </summary>
    void Reset();

    /// <summary>
    /// Gets the speech detection threshold.
    /// </summary>
    float SpeechDetectionThreshold { get; }
}
