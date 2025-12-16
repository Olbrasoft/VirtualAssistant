using System.Text.Json.Serialization;

namespace VirtualAssistant.Data.Dtos.Tasks;

/// <summary>
/// Request model for send task endpoint.
/// </summary>
public class SendTaskRequest
{
    /// <summary>
    /// Source agent name (who is sending the task).
    /// </summary>
    [JsonPropertyName("source_agent")]
    public string SourceAgent { get; set; } = string.Empty;

    /// <summary>
    /// Target agent name (who should receive the task).
    /// </summary>
    [JsonPropertyName("target_agent")]
    public string TargetAgent { get; set; } = string.Empty;

    /// <summary>
    /// Task content/description.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Optional GitHub issue reference (e.g., "#123" or "123").
    /// </summary>
    [JsonPropertyName("github_issue")]
    public string? GithubIssue { get; set; }

    /// <summary>
    /// Optional priority: "normal" (default) or "high".
    /// High priority tasks skip approval.
    /// </summary>
    [JsonPropertyName("priority")]
    public string? Priority { get; set; }
}
