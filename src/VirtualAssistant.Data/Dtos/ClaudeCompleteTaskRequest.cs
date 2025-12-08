using System.Text.Json.Serialization;

namespace VirtualAssistant.Data.Dtos;

/// <summary>
/// Request model for POST /api/claude/tasks/{id}/complete endpoint.
/// Used by Claude Code to mark a task as completed.
/// </summary>
public class ClaudeCompleteTaskRequest
{
    /// <summary>
    /// Claude Code session ID from the execution.
    /// </summary>
    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }

    /// <summary>
    /// Result summary of the task completion.
    /// </summary>
    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;
}
