using System.Text.Json.Serialization;

namespace VirtualAssistant.Data.Dtos.Tasks;

/// <summary>
/// Request model for dispatch task endpoint.
/// </summary>
public class DispatchTaskRequest
{
    /// <summary>
    /// Target agent (default: "claude").
    /// </summary>
    [JsonPropertyName("target_agent")]
    public string? TargetAgent { get; set; }

    /// <summary>
    /// Optional specific GitHub issue number.
    /// </summary>
    [JsonPropertyName("github_issue_number")]
    public int? GithubIssueNumber { get; set; }

    /// <summary>
    /// Optional specific GitHub issue URL.
    /// </summary>
    [JsonPropertyName("github_issue_url")]
    public string? GithubIssueUrl { get; set; }
}
