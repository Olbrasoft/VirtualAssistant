using Olbrasoft.Data.Entities.Abstractions;

namespace VirtualAssistant.Data.Entities;

/// <summary>
/// Log entry for agent message operations.
/// Used to track errors, warnings, and debug info for hub message processing.
/// </summary>
public class AgentMessageLog : BaseEnity
{
    /// <summary>
    /// Source agent that triggered the log entry
    /// </summary>
    public string SourceAgent { get; set; } = string.Empty;

    /// <summary>
    /// Log level: "error", "warning", "info", "debug"
    /// </summary>
    public string Level { get; set; } = "info";

    /// <summary>
    /// Log message describing the event
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional JSON context with additional details
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// When the log entry was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
