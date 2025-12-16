using System.Text.Json.Serialization;

namespace VirtualAssistant.LlmChain.Dtos;

/// <summary>
/// Choice model from LLM API response.
/// </summary>
public class LlmApiChoice
{
    [JsonPropertyName("message")]
    public LlmApiMessage? Message { get; set; }
}
