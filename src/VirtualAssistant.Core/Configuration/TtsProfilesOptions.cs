namespace Olbrasoft.VirtualAssistant.Core.Configuration;

/// <summary>
/// Configuration options for application-specific TTS profiles.
/// Each application can have its own voice, rate, pitch, and provider preferences.
/// </summary>
public class TtsProfilesOptions
{
    public const string SectionName = "TtsProfiles";

    /// <summary>
    /// Application-specific TTS profiles keyed by application name (e.g., "claude-code", "desktop-monitor").
    /// </summary>
    public Dictionary<string, TtsProfile> Profiles { get; set; } = new();

    /// <summary>
    /// Default TTS profile used when application name is unknown or profile not found.
    /// </summary>
    public TtsProfile DefaultProfile { get; set; } = new()
    {
        Provider = "Piper", // Local fallback
        Voice = "cs_CZ-jirka-medium",
        Rate = 0,
        Pitch = 0,
        Priority = 1
    };
}

/// <summary>
/// TTS profile configuration for a specific application.
/// </summary>
public class TtsProfile
{
    /// <summary>
    /// Preferred TTS provider (e.g., "Azure", "EdgeTTS", "Piper").
    /// Default: "Piper" (local offline TTS).
    /// </summary>
    public string Provider { get; set; } = "Piper";

    /// <summary>
    /// Voice name/identifier for the TTS provider.
    /// Examples: "cs-CZ-AntoninNeural" (Azure), "cs-CZ-VlastaNeural" (EdgeTTS).
    /// </summary>
    public string Voice { get; set; } = string.Empty;

    /// <summary>
    /// Speech rate adjustment. Range: -100 to +100.
    /// 0 = normal speed, positive = faster, negative = slower.
    /// </summary>
    public int Rate { get; set; } = 0;

    /// <summary>
    /// Voice pitch adjustment. Range: -100 to +100.
    /// 0 = normal pitch, positive = higher, negative = lower.
    /// </summary>
    public int Pitch { get; set; } = 0;

    /// <summary>
    /// Queue priority for notifications from this application.
    /// Higher values = higher priority. Default: 1.
    /// </summary>
    public int Priority { get; set; } = 1;
}
