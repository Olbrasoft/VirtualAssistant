using System.Text.Json.Serialization;

namespace Olbrasoft.VirtualAssistant.LlmChain.Dtos;

/// <summary>
/// Response model from LLM API.
/// </summary>
public class LlmApiResponse
{
    [JsonPropertyName("choices")]
    public LlmApiChoice[]? Choices { get; set; }
}
