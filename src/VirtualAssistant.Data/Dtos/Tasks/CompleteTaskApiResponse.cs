using System.Text.Json.Serialization;

namespace VirtualAssistant.Data.Dtos.Tasks;

/// <summary>
/// Response model for complete-task endpoint.
/// </summary>
public class CompleteTaskApiResponse
{
    /// <summary>
    /// Whether the completion was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Task ID that was completed.
    /// </summary>
    [JsonPropertyName("task_id")]
    public int TaskId { get; set; }

    /// <summary>
    /// Confirmation message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Information about the next task that was auto-dispatched (if any).
    /// </summary>
    [JsonPropertyName("next_task")]
    public NextTaskInfo? NextTask { get; set; }
}
