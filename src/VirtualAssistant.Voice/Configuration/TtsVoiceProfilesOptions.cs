using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Configuration;

/// <summary>
/// Configuration options for TTS voice profiles.
/// Allows configuring different voice settings for different AI clients.
/// </summary>
public class TtsVoiceProfilesOptions
{
    public const string SectionName = "TtsVoiceProfiles";

    /// <summary>
    /// Dictionary of voice profiles keyed by client identifier (e.g., "default", "opencode", "claudecode").
    /// Each profile defines voice, rate, volume, and pitch settings.
    /// </summary>
    public Dictionary<string, VoiceConfig> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        // Default voice profile - normal Antonín
        ["default"] = new("cs-CZ-AntoninNeural", "+20%", "+0%", "+0Hz"),

        // OpenCode - faster, higher pitch (energetic)
        ["opencode"] = new("cs-CZ-AntoninNeural", "+25%", "+0%", "+3st"),

        // Claude Code - deeper voice, slower (authoritative)
        ["claudecode"] = new("cs-CZ-AntoninNeural", "+5%", "+0%", "-5st"),
    };
}
