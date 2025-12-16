using System.Text.Json.Serialization;

namespace VirtualAssistant.Data.Dtos.Tasks;

/// <summary>
/// Information about an auto-dispatched task.
/// </summary>
public class NextTaskInfo
{
    /// <summary>
    /// Task ID of the next task.
    /// </summary>
    [JsonPropertyName("task_id")]
    public int TaskId { get; set; }

    /// <summary>
    /// GitHub issue number (if associated).
    /// </summary>
    [JsonPropertyName("github_issue_number")]
    public int? GithubIssueNumber { get; set; }

    /// <summary>
    /// Task summary/description.
    /// </summary>
    public string? Summary { get; set; }
}
