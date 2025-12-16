using System.Text.Json.Serialization;

namespace Olbrasoft.VirtualAssistant.Voice.Dtos.Llm;

/// <summary>
/// Request model for LLM API calls.
/// </summary>
public class LlmRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; set; }

    [JsonPropertyName("messages")]
    public required LlmMessage[] Messages { get; set; }

    [JsonPropertyName("temperature")]
    public float Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }
}
