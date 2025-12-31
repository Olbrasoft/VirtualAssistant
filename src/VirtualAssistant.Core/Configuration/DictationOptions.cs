namespace Olbrasoft.VirtualAssistant.Core.Configuration;

/// <summary>
/// Configuration options for keyboard-triggered Dictation workflow.
/// </summary>
public class DictationOptions
{
    public const string SectionName = "Dictation";

    /// <summary>
    /// Path to Whisper model file for dictation transcription.
    /// Can be just filename (e.g., "ggml-large-v3-turbo.bin") which will be resolved using WhisperModelLocator.
    /// Default: ggml-large-v3-turbo.bin (more accurate than medium used for ContinuousListener).
    /// </summary>
    public string WhisperModelPath { get; set; } = "ggml-large-v3-turbo.bin";

    /// <summary>
    /// Language for Whisper transcription. Default: cs (Czech).
    /// </summary>
    public string WhisperLanguage { get; set; } = "cs";

    /// <summary>
    /// Audio sample rate in Hz. Default: 16000.
    /// </summary>
    public int SampleRate { get; set; } = 16000;

    /// <summary>
    /// Keyboard LED settle time in milliseconds.
    /// Time to wait for keyboard LED state to stabilize before checking Caps Lock/Num Lock state.
    /// Default: 50 ms.
    /// </summary>
    public int KeyboardLedSettleTimeMs { get; set; } = 50;

    /// <summary>
    /// Maximum dictation duration in seconds.
    /// Default: 300 seconds (5 minutes).
    /// </summary>
    public int MaxDurationSeconds { get; set; } = 300;

    /// <summary>
    /// Gets the full path for WhisperModelPath, resolving relative paths.
    /// If WhisperModelPath is just a filename (no path separators), uses WhisperModelLocator
    /// to find the model in FHS-compliant locations (~/.local/share/whisper-models/).
    /// </summary>
    public string GetFullWhisperModelPath()
    {
        // If it's just a filename (no path separators), use WhisperModelLocator
        if (!Path.IsPathRooted(WhisperModelPath) && !WhisperModelPath.Contains('/') && !WhisperModelPath.Contains('\\'))
        {
            return WhisperModelLocator.GetModelPath(WhisperModelPath);
        }

        // Legacy: absolute path or relative to base directory
        return Path.IsPathRooted(WhisperModelPath)
            ? WhisperModelPath
            : Path.Combine(AppContext.BaseDirectory, WhisperModelPath);
    }
}
