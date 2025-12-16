using System.Text.Json.Serialization;

namespace Olbrasoft.VirtualAssistant.Voice.Dtos.Llm;

/// <summary>
/// Choice model from LLM API response.
/// </summary>
public class LlmChoice
{
    [JsonPropertyName("message")]
    public LlmMessage? Message { get; set; }
}
