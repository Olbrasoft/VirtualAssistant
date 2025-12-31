namespace Olbrasoft.VirtualAssistant.Voice.Services.EchoDetection;

/// <summary>
/// Strategy for detecting and removing echo from transcriptions.
/// </summary>
public interface IEchoDetectionStrategy
{
    /// <summary>
    /// Attempts to detect and remove echo from transcription.
    /// </summary>
    /// <param name="transcription">The transcription text to check.</param>
    /// <param name="ttsMessage">The TTS message to compare against.</param>
    /// <returns>
    /// Result containing:
    /// - wasRemoved: true if echo was detected and removed
    /// - remainingText: text after echo removal
    /// - similarity: confidence score (0.0 - 1.0)
    /// </returns>
    (bool wasRemoved, string remainingText, double similarity) DetectAndRemove(string transcription, string ttsMessage);

    /// <summary>
    /// Gets the strategy name for logging purposes.
    /// </summary>
    string StrategyName { get; }
}
