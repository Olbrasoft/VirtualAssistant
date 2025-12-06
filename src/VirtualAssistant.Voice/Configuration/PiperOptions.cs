namespace Olbrasoft.VirtualAssistant.Voice.Configuration;

/// <summary>
/// Configuration options for Piper TTS provider.
/// </summary>
public sealed class PiperOptions
{
    public const string SectionName = "Piper";

    /// <summary>
    /// Path to the piper binary. If not set, assumes 'piper' is in PATH.
    /// </summary>
    public string PiperPath { get; set; } = "piper";

    /// <summary>
    /// Path to the ONNX model file.
    /// Default: ~/virtual-assistant/piper-voices/cs/cs_CZ-jirka-medium.onnx
    /// </summary>
    public string ModelPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "virtual-assistant", "piper-voices", "cs", "cs_CZ-jirka-medium.onnx");

    /// <summary>
    /// Path to the model config file (.json). Optional.
    /// </summary>
    public string? ConfigPath { get; set; }

    /// <summary>
    /// Length scale for speech rate. Lower = faster, higher = slower.
    /// Default: 1.0 (normal speed)
    /// </summary>
    public float? LengthScale { get; set; }
}
