using System.Text.Json.Serialization;

namespace VirtualAssistant.LlmChain.Dtos;

/// <summary>
/// Request model for LLM API calls.
/// </summary>
public class LlmApiRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; set; }

    [JsonPropertyName("messages")]
    public required LlmApiMessage[] Messages { get; set; }

    [JsonPropertyName("temperature")]
    public float Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }
}
