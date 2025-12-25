namespace Olbrasoft.VirtualAssistant.Service.Controllers;

/// <summary>
/// Request model for creating an advanced notification with TTS options.
/// Used by the new /api/notifications/advanced endpoint.
/// </summary>
public class CreateAdvancedNotificationRequest
{
    /// <summary>
    /// Notification text content.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Agent/program name (e.g., "Claude Code", "Gemini").
    /// </summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier for this agent instance.
    /// When multiple instances of the same agent run, each has a unique ID.
    /// </summary>
    public string? AgentInstanceId { get; set; }

    /// <summary>
    /// Voice name to use for this notification (e.g., "cs-CZ-Chirp3-HD-Achird").
    /// Overrides default voice selection.
    /// </summary>
    public string? Voice { get; set; }

    /// <summary>
    /// Ordered list of TTS provider names to try (e.g., ["GoogleCloud", "AzureTTS", "EdgeTTS"]).
    /// Overrides default provider chain from configuration.
    /// </summary>
    public List<string>? ProviderFallbackChain { get; set; }

    /// <summary>
    /// Optional GitHub issue IDs to associate with this notification.
    /// </summary>
    public List<int>? IssueIds { get; set; }
}
