using System.Text.Json.Serialization;

namespace VirtualAssistant.LlmChain.Dtos;

/// <summary>
/// Message model for LLM API requests and responses.
/// </summary>
public class LlmApiMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; set; }

    [JsonPropertyName("content")]
    public required string Content { get; set; }
}
