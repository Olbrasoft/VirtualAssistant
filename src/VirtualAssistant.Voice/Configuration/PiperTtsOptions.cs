namespace Olbrasoft.VirtualAssistant.Voice.Configuration;

/// <summary>
/// Configuration options for Piper TTS (offline fallback).
/// </summary>
public class PiperTtsOptions
{
    public const string SectionName = "PiperTts";

    /// <summary>
    /// Path to the Piper voice model (.onnx file).
    /// </summary>
    public string ModelPath { get; set; } = "/home/jirka/virtual-assistant/piper-voices/cs/cs_CZ-jirka-medium.onnx";

    /// <summary>
    /// Dictionary of voice profiles keyed by client identifier (e.g., "default", "claudecode").
    /// Each profile defines speed and other voice parameters.
    /// </summary>
    public Dictionary<string, PiperVoiceConfig> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        // Default profile - faster speech (0.5 = 2x speed)
        ["default"] = new()
    };
}

/// <summary>
/// Piper voice configuration for a specific profile.
/// </summary>
public class PiperVoiceConfig
{
    /// <summary>
    /// Phoneme length scale. Lower = faster speech.
    /// Default: 1.0, Recommended for faster: 0.5
    /// </summary>
    public double LengthScale { get; set; } = 0.5;

    /// <summary>
    /// Generator noise scale. Controls voice variation.
    /// Default: 0.667
    /// </summary>
    public double NoiseScale { get; set; } = 0.667;

    /// <summary>
    /// Phoneme width noise scale. Controls pronunciation variation.
    /// Default: 0.8
    /// </summary>
    public double NoiseWScale { get; set; } = 0.8;

    /// <summary>
    /// Seconds of silence after each sentence.
    /// Default: 0.2
    /// </summary>
    public double SentenceSilence { get; set; } = 0.2;

    /// <summary>
    /// Volume multiplier.
    /// Default: 1.0
    /// </summary>
    public double Volume { get; set; } = 1.0;

    /// <summary>
    /// Speaker ID (for multi-speaker models).
    /// Default: 0
    /// </summary>
    public int Speaker { get; set; } = 0;
}
