using System.Text.Json.Serialization;

namespace Olbrasoft.VirtualAssistant.Voice.Dtos.Llm;

/// <summary>
/// Response DTO from LLM router.
/// </summary>
public class LlmRouterResponseDto
{
    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("prompt_type")]
    public string? PromptType { get; set; }

    [JsonPropertyName("confidence")]
    public float Confidence { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("response")]
    public string? Response { get; set; }

    [JsonPropertyName("command_for_opencode")]
    public string? CommandForOpenCode { get; set; }

    [JsonPropertyName("bash_command")]
    public string? BashCommand { get; set; }

    [JsonPropertyName("note_title")]
    public string? NoteTitle { get; set; }

    [JsonPropertyName("note_content")]
    public string? NoteContent { get; set; }

    [JsonPropertyName("discussion_topic")]
    public string? DiscussionTopic { get; set; }

    [JsonPropertyName("target_agent")]
    public string? TargetAgent { get; set; }
}
