namespace Olbrasoft.VirtualAssistant.Voice.Configuration;

/// <summary>
/// Configuration options for echo detection in speech tracking.
/// </summary>
public class EchoDetectionOptions
{
    /// <summary>
    /// Similarity threshold for fuzzy matching (0.0 - 1.0).
    /// Default: 0.70 (70% similarity required).
    /// </summary>
    public double SimilarityThreshold { get; set; } = 0.70;

    /// <summary>
    /// Maximum speaking duration in seconds before considering TTS stale.
    /// Default: 60 seconds.
    /// </summary>
    public int MaxSpeakingDurationSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum number of TTS messages to keep in history.
    /// Default: 10 messages.
    /// </summary>
    public int MaxHistorySize { get; set; } = 10;

    /// <summary>
    /// Minimum word count for similarity-based prefix matching.
    /// Default: 3 words.
    /// </summary>
    public int MinimumWordCount { get; set; } = 3;

    /// <summary>
    /// Minimum word similarity for fuzzy word matching.
    /// Default: 0.80 (80% similarity required).
    /// </summary>
    public double WordSimilarityThreshold { get; set; } = 0.80;

    /// <summary>
    /// Minimum ratio of TTS words that must match for prefix detection.
    /// Default: 0.60 (60% of TTS words must match).
    /// </summary>
    public double TtsMatchRatioThreshold { get; set; } = 0.60;
}
