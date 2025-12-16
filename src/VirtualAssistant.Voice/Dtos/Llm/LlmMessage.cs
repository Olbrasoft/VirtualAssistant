using System.Text.Json.Serialization;

namespace Olbrasoft.VirtualAssistant.Voice.Dtos.Llm;

/// <summary>
/// Message model for LLM API.
/// </summary>
public class LlmMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; set; }

    [JsonPropertyName("content")]
    public required string Content { get; set; }
}
