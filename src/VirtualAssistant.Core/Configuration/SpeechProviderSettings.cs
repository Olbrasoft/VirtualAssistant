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
    /// Primary STT provider name ("google" or "whisper"). Defaults to local
    /// Whisper — no remote API round-trip for the default deployment.
    /// </summary>
    public string PrimaryProvider { get; set; } = "whisper";

    /// <summary>
    /// Fallback STT provider name when primary fails. Defaults to "google"
    /// since Whisper is primary by default.
    /// </summary>
    public string FallbackProvider { get; set; } = "google";

    /// <summary>
    /// Enable automatic fallback to secondary provider on failure.
    /// </summary>
    public bool EnableFallback { get; set; } = true;
}
