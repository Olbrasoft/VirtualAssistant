namespace Olbrasoft.VirtualAssistant.Core.Configuration;

/// <summary>
/// Configuration settings for speech-to-text provider selection.
/// </summary>
public class SpeechProviderSettings
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "SpeechProvider";

    /// <summary>
    /// Primary STT provider name ("google" or "whisper").
    /// </summary>
    public string PrimaryProvider { get; set; } = "google";

    /// <summary>
    /// Fallback STT provider name when primary fails.
    /// </summary>
    public string FallbackProvider { get; set; } = "whisper";

    /// <summary>
    /// Enable automatic fallback to secondary provider on failure.
    /// </summary>
    public bool EnableFallback { get; set; } = true;
}
