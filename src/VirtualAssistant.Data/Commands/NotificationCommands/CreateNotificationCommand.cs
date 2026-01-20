namespace Olbrasoft.VirtualAssistant.Data.Commands.NotificationCommands;

/// <summary>
/// Command to create a new notification in the database.
/// </summary>
/// <param name="Text">Notification text content.</param>
/// <param name="AgentName">Agent name (e.g., "opencode", "claude", "gemini").</param>
/// <param name="IssueIds">Optional GitHub issue IDs to associate with this notification.</param>
/// <param name="ProviderName">Optional LLM provider name (e.g., "anthropic", "openai"). Will be auto-created if not exists.</param>
/// <param name="ModelName">Optional LLM model identifier (e.g., "claude-opus-4-5-20251101"). Will be auto-created if not exists.</param>
public record CreateNotificationCommand(
    string Text,
    string AgentName,
    IReadOnlyList<int>? IssueIds = null,
    string? ProviderName = null,
    string? ModelName = null
) : ICommand<int>;
