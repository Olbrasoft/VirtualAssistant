namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Interface for Text-to-Speech providers.
/// Allows different TTS backends (Edge TTS, Piper, etc.) to be used interchangeably.
/// </summary>
public interface ITtsProvider
{
    /// <summary>
    /// Gets the name of the provider for logging purposes.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Checks if this provider is currently available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the provider can be used</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates audio data from text.
    /// </summary>
    /// <param name="text">Text to convert to speech</param>
    /// <param name="voiceConfig">Voice configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Audio data as byte array, or null if generation failed</returns>
    Task<byte[]?> GenerateAudioAsync(string text, VoiceConfig voiceConfig, CancellationToken cancellationToken = default);
}
