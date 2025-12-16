using System.Text.Json.Serialization;

namespace VirtualAssistant.Data.Dtos.Tasks;

/// <summary>
/// Request model for complete-task endpoint.
/// </summary>
public class CompleteTaskApiRequest
{
    /// <summary>
    /// Task ID to complete.
    /// </summary>
    [JsonPropertyName("task_id")]
    public int? TaskId { get; set; }

    /// <summary>
    /// Alternative: GitHub issue number.
    /// </summary>
    [JsonPropertyName("github_issue_number")]
    public int? GithubIssueNumber { get; set; }

    /// <summary>
    /// Result/outcome description.
    /// </summary>
    [JsonPropertyName("result")]
    public string? Result { get; set; }

    /// <summary>
    /// Status: "completed", "failed", or "blocked".
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Whether to automatically dispatch the next pending task after completion.
    /// Default: true.
    /// </summary>
    [JsonPropertyName("auto_dispatch")]
    public bool? AutoDispatch { get; set; }
}
