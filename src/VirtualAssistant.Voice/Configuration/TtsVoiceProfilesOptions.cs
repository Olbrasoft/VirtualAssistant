using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Configuration;

/// <summary>
/// Configuration options for TTS voice.
/// Single voice configuration for the Virtual Assistant.
/// </summary>
public class TtsVoiceProfilesOptions
{
    public const string SectionName = "TtsVoice";

    /// <summary>
    /// Voice name (e.g., "cs-CZ-AntoninNeural").
    /// </summary>
    public string Voice { get; set; } = "cs-CZ-AntoninNeural";

    /// <summary>
    /// Speech rate (e.g., "+10%", "-5%", "default").
    /// </summary>
    public string Rate { get; set; } = "+10%";

    /// <summary>
    /// Volume (e.g., "+0%", "default").
    /// </summary>
    public string Volume { get; set; } = "+0%";

    /// <summary>
    /// Pitch (e.g., "+0Hz", "-5st", "default").
    /// </summary>
    public string Pitch { get; set; } = "+0Hz";

    /// <summary>
    /// Creates VoiceConfig from these options.
    /// </summary>
    public VoiceConfig ToVoiceConfig() => new(Voice, Rate, Volume, Pitch);
}
