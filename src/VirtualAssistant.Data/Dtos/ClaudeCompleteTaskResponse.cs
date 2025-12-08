namespace VirtualAssistant.Data.Dtos;

/// <summary>
/// Response model for POST /api/claude/tasks/{id}/complete endpoint.
/// Confirms task completion.
/// </summary>
public class ClaudeCompleteTaskResponse
{
    /// <summary>
    /// Task ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Task status (should be "completed").
    /// </summary>
    public string Status { get; set; } = "completed";

    /// <summary>
    /// When the task was completed.
    /// </summary>
    public DateTime CompletedAt { get; set; }
}
