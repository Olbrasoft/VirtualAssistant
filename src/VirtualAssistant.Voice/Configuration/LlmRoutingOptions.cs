using System.ComponentModel.DataAnnotations;

namespace Olbrasoft.VirtualAssistant.Voice.Configuration;

/// <summary>
/// Configuration options for LLM routing.
/// </summary>
public class LlmRoutingOptions
{
    public const string SectionName = "LlmRouting";

    /// <summary>
    /// Maximum number of context entries to keep for multi-turn awareness.
    /// Default: 5.
    /// </summary>
    [Range(1, 20, ErrorMessage = "MaxContextEntries must be between 1 and 20")]
    public int MaxContextEntries { get; set; } = 5;

    /// <summary>
    /// LLM temperature for response generation (0.0 = deterministic, 1.0 = creative).
    /// Default: 0.2 (mostly deterministic with slight variation).
    /// </summary>
    [Range(0.0, 2.0, ErrorMessage = "Temperature must be between 0.0 and 2.0")]
    public float Temperature { get; set; } = 0.2f;

    /// <summary>
    /// Maximum number of tokens to generate in LLM response.
    /// Default: 256 tokens.
    /// </summary>
    [Range(1, 4096, ErrorMessage = "MaxTokens must be between 1 and 4096")]
    public int MaxTokens { get; set; } = 256;
}
