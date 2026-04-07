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
    /// Floor / minimum number of tokens to allocate for the LLM response.
    /// Each provider dynamically calculates the actual <c>max_tokens</c> sent
    /// to the API based on the input length (see
    /// <c>LlmProviderBase.CalculateMaxTokens</c>); this value acts as a
    /// lower bound so short dictations still get a reasonable budget.
    /// Default: 1000 tokens.
    /// </summary>
    [Range(1, 16384, ErrorMessage = "MaxTokens must be between 1 and 16384")]
    public int MaxTokens { get; set; } = 1000;
}
