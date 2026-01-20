namespace Olbrasoft.VirtualAssistant.Data.Commands.NotificationCommands;

/// <summary>
/// Command to create a new notification in the database.
/// </summary>
/// <param name="Text">Notification text content.</param>
/// <param name="AgentName">Agent name (e.g., "opencode", "claude", "gemini").</param>
/// <param name="IssueIds">Optional GitHub issue IDs to associate with this notification.</param>
/// <param name="ProviderName">
/// Optional LLM provider name (e.g., "anthropic", "openai"). Auto-created if not exists.
/// Can be used independently or together with ModelName.
/// </param>
/// <param name="ModelName">
/// Optional LLM model identifier (e.g., "claude-opus-4-5-20251101"). Auto-created if not exists.
/// Can be used independently or together with ProviderName. When both provided, a mapping is created.
/// </param>
public record CreateNotificationCommand(
    string Text,
    string AgentName,
    IReadOnlyList<int>? IssueIds = null,
    string? ProviderName = null,
    string? ModelName = null
) : ICommand<int>;
