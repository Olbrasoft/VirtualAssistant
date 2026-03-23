namespace Olbrasoft.VirtualAssistant.Voice.Configuration;

/// <summary>
/// Configuration options for Inception Labs Mercury 2 API.
/// Mercury 2 is a diffusion-based LLM with OpenAI-compatible API.
/// </summary>
public class MercuryOptions : ILlmProviderOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Mercury";

    /// <summary>
    /// Inception Labs API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Mercury API base URL.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.inceptionlabs.ai/v1/";

    /// <summary>
    /// Model name to use for corrections.
    /// </summary>
    public string Model { get; set; } = "mercury-2";

    /// <summary>
    /// Controls reasoning depth for Mercury 2.
    /// Supported values: "instant" (no reasoning), "low", "medium", "high".
    /// Use "low" or "instant" for ASR correction (fastest).
    /// </summary>
    public string ReasoningEffort { get; set; } = "low";

    /// <summary>
    /// API timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of tokens in response.
    /// </summary>
    public int MaxTokens { get; set; } = 1000;

    /// <summary>
    /// Temperature for text generation.
    /// Mercury 2 supports range 0.5-1.0 only. Values below 0.5 are clamped.
    /// </summary>
    public double Temperature { get; set; } = 0.5;

    /// <summary>
    /// Minimum text length (in characters) required before sending to LLM for correction.
    /// </summary>
    public int MinTextLengthForCorrection { get; set; } = 21;

    /// <summary>
    /// Enable or disable sending transcriptions to LLM for correction.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
