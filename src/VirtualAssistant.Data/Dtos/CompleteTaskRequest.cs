namespace VirtualAssistant.Data.Dtos;

/// <summary>
/// Request DTO for completing a task.
/// </summary>
public class CompleteTaskRequest
{
    /// <summary>
    /// ID of the task to complete.
    /// </summary>
    public int TaskId { get; set; }

    /// <summary>
    /// Result/outcome description.
    /// </summary>
    public string Result { get; set; } = string.Empty;
}
