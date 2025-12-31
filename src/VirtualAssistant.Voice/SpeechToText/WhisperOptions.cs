namespace Olbrasoft.VirtualAssistant.Voice.SpeechToText;

/// <summary>
/// Configuration options for Whisper.net transcription.
/// </summary>
public sealed class WhisperOptions
{
    /// <summary>
    /// Gets or sets the Whisper model file name or path.
    /// If it's just a filename, WhisperModelLocator will find it in standard locations.
    /// Model must be explicitly specified - no default model.
    /// </summary>
    public string? ModelPath { get; set; } = null;

    /// <summary>
    /// Gets or sets the default language for transcription (e.g., "cs", "en").
    /// Null means auto-detection.
    /// </summary>
    public string? DefaultLanguage { get; set; } = "cs";

    /// <summary>
    /// Gets or sets whether to use GPU acceleration (CUDA).
    /// </summary>
    public bool UseGpu { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of concurrent transcription requests.
    /// Whisper.net is not thread-safe, so we use a semaphore to limit concurrency.
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 3;

    /// <summary>
    /// Gets the full path to the Whisper model.
    /// If ModelPath is just a filename, uses WhisperModelLocator.
    /// </summary>
    public string GetFullModelPath()
    {
        if (string.IsNullOrWhiteSpace(ModelPath))
        {
            throw new InvalidOperationException(
                "ModelPath is not configured. " +
                "Please set Whisper:ModelPath in appsettings.json or environment variables.");
        }

        // If it's just a filename (no path separators), use WhisperModelLocator
        if (!Path.IsPathRooted(ModelPath) && !ModelPath.Contains('/') && !ModelPath.Contains('\\'))
        {
            return WhisperModelLocator.GetModelPath(ModelPath);
        }

        // Legacy: absolute path or relative to base directory
        return Path.IsPathRooted(ModelPath)
            ? ModelPath
            : Path.Combine(AppContext.BaseDirectory, ModelPath);
    }
}
