using System.Text.Json.Serialization;

namespace Olbrasoft.VirtualAssistant.Voice.Dtos.Llm;

/// <summary>
/// Response model from LLM API.
/// </summary>
public class LlmResponse
{
    [JsonPropertyName("choices")]
    public LlmChoice[]? Choices { get; set; }
}
