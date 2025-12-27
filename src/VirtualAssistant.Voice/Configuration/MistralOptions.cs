namespace Olbrasoft.VirtualAssistant.Voice.Configuration;

/// <summary>
/// Configuration options for Mistral API.
/// </summary>
public class MistralOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Mistral";

    /// <summary>
    /// Mistral API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Mistral API base URL.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.mistral.ai";

    /// <summary>
    /// Model name to use for corrections.
    /// </summary>
    public string Model { get; set; } = "mistral-large-latest";

    /// <summary>
    /// API timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of tokens in response.
    /// </summary>
    public int MaxTokens { get; set; } = 1000;

    /// <summary>
    /// Temperature for text generation (0.0 - 1.0).
    /// Lower = more deterministic.
    /// </summary>
    public double Temperature { get; set; } = 0.3;

    /// <summary>
    /// Minimum text length (in characters) required before sending to LLM for correction.
    /// Texts shorter than this threshold will skip LLM correction and return unchanged.
    /// Default: 21 characters.
    /// </summary>
    public int MinTextLengthForCorrection { get; set; } = 21;

    /// <summary>
    /// Enable or disable sending transcriptions to LLM for correction.
    /// When disabled, all transcriptions are returned unchanged (Whisper output only).
    /// Default: true (enabled).
    /// </summary>
    public bool Enabled { get; set; } = true;
}
