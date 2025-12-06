namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Interface for Text-to-Speech providers.
/// Enables provider pattern for different TTS backends (Edge, Piper, etc.)
/// </summary>
public interface ITtsProvider
{
    /// <summary>
    /// Gets the name of this TTS provider.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets whether this provider is currently available.
    /// Can be used for fallback logic when primary provider is unavailable.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Generates audio data from text using this TTS provider.
    /// </summary>
    /// <param name="text">Text to synthesize</param>
    /// <param name="config">Voice configuration (voice, rate, volume, pitch)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Audio data as bytes (MP3 format), or null if generation failed</returns>
    Task<byte[]?> GenerateAudioAsync(string text, VoiceConfig config, CancellationToken cancellationToken);
}
