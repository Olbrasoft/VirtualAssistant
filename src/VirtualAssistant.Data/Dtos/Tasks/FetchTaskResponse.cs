namespace VirtualAssistant.Data.Dtos.Tasks;

/// <summary>
/// Response model for fetch task endpoint.
/// </summary>
public class FetchTaskResponse
{
    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The fetched task, or null if no tasks available.
    /// </summary>
    public FetchedTaskInfo? Task { get; set; }

    /// <summary>
    /// Optional message (e.g., "No pending tasks").
    /// </summary>
    public string? Message { get; set; }
}
