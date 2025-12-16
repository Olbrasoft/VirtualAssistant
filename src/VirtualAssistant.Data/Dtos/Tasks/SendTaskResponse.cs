using System.Text.Json.Serialization;

namespace VirtualAssistant.Data.Dtos.Tasks;

/// <summary>
/// Response model for send task endpoint.
/// </summary>
public class SendTaskResponse
{
    /// <summary>
    /// Whether the task was queued successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// ID of the created task.
    /// </summary>
    [JsonPropertyName("task_id")]
    public int TaskId { get; set; }

    /// <summary>
    /// Confirmation message.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
