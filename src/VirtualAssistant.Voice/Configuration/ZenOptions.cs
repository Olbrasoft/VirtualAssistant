namespace Olbrasoft.VirtualAssistant.Voice.Configuration;

/// <summary>
/// Configuration options for OpenCode Zen API gateway.
/// Supports models: kimi-k2, glm-4.7 (with reasoning_effort), claude-3-5-haiku, etc.
/// </summary>
public class ZenOptions : ILlmProviderOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Zen";

    /// <summary>
    /// Zen API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Zen API base URL.
    /// </summary>
    public string BaseUrl { get; set; } = "https://opencode.ai/zen/v1";

    /// <summary>
    /// Model name to use for corrections.
    /// Supported: kimi-k2, glm-4.7, claude-3-5-haiku, claude-haiku-4-5, big-pickle.
    /// </summary>
    public string Model { get; set; } = "glm-4.7";

    /// <summary>
    /// Controls reasoning/thinking mode for models that support it.
    /// Set to "none" to disable reasoning (required for glm-4.7 to get clean output).
    /// Leave null/empty for models that don't need it (e.g. kimi-k2).
    /// Supported values: "none", "low", "medium", "high", or null.
    /// </summary>
    public string? ReasoningEffort { get; set; } = "none";

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
