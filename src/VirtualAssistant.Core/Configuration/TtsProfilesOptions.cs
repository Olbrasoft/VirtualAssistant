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
        Rate = "+0%",
        Pitch = "+0Hz",
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
    /// NOTE: Currently not used in MapToVoiceConfig - reserved for future provider selection logic.
    /// </summary>
    public string Provider { get; set; } = "Piper";

    /// <summary>
    /// Voice name/identifier for the TTS provider.
    /// Examples: "cs-CZ-AntoninNeural" (Azure), "cs-CZ-VlastaNeural" (EdgeTTS).
    /// </summary>
    public string Voice { get; set; } = string.Empty;

    /// <summary>
    /// Speech rate adjustment with unit suffix.
    /// Format: "+N%" or "-N%" where N is 0-100.
    /// Examples: "+10%" (10% faster), "+0%" (normal), "-5%" (5% slower).
    /// </summary>
    public string Rate { get; set; } = "+0%";

    /// <summary>
    /// Voice pitch adjustment with unit suffix.
    /// Format: "+NHz" or "-NHz" where N is 0-100.
    /// Examples: "+5Hz" (higher pitch), "+0Hz" (normal), "-10Hz" (lower pitch).
    /// </summary>
    public string Pitch { get; set; } = "+0Hz";

    /// <summary>
    /// Queue priority for notifications from this application.
    /// Higher values = higher priority. Default: 1.
    /// NOTE: Currently not used - reserved for future priority queue implementation.
    /// </summary>
    public int Priority { get; set; } = 1;
}
