namespace VirtualAssistant.Core.Services;

/// <summary>
/// Service for humanizing agent notifications using LLM.
/// Transforms raw agent messages into natural Czech speech.
/// </summary>
public interface IHumanizationService
{
    /// <summary>
    /// Humanizes a batch of agent notifications into natural Czech speech.
    /// </summary>
    /// <param name="notifications">List of notifications to humanize</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Humanized text for TTS, or null if no notification needed</returns>
    Task<string?> HumanizeAsync(IReadOnlyList<AgentNotification> notifications, CancellationToken ct = default);
}

/// <summary>
/// Represents an agent notification to be humanized.
/// </summary>
public record AgentNotification
{
    /// <summary>
    /// Database ID of the notification (for status tracking).
    /// </summary>
    public int? NotificationId { get; init; }

    /// <summary>
    /// Name of the agent (e.g., "opencode", "claudecode")
    /// </summary>
    public required string Agent { get; init; }

    /// <summary>
    /// Type of notification (e.g., "start", "complete", "progress")
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Raw content of the notification
    /// </summary>
    public required string Content { get; init; }
}
