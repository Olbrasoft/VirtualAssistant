namespace VirtualAssistant.Data.Dtos;

/// <summary>
/// Response from unified task create-and-dispatch operation.
/// </summary>
public class CreateAndDispatchResponse
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// What action was taken: "created", "reopened", "dispatched".
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Task ID.
    /// </summary>
    public int? TaskId { get; set; }

    /// <summary>
    /// GitHub issue number.
    /// </summary>
    public int? GithubIssueNumber { get; set; }

    /// <summary>
    /// Dispatch status: "sent_to_claude", "queued".
    /// </summary>
    public string? DispatchStatus { get; set; }

    /// <summary>
    /// Previous task status (when reopening).
    /// </summary>
    public string? PreviousStatus { get; set; }

    /// <summary>
    /// Failure reason (when not successful).
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Human-readable message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Error message (when not successful).
    /// </summary>
    public string? Error { get; set; }
}
