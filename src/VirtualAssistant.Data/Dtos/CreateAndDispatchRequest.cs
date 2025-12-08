namespace VirtualAssistant.Data.Dtos;

/// <summary>
/// Request for unified task create-and-dispatch operation.
/// </summary>
public class CreateAndDispatchRequest
{
    /// <summary>
    /// GitHub issue number (required).
    /// </summary>
    public int? GithubIssueNumber { get; set; }

    /// <summary>
    /// Task summary/description.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Target agent name (default: "claude").
    /// </summary>
    public string? TargetAgent { get; set; }
}
