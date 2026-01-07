namespace Olbrasoft.VirtualAssistant.Data.Commands.NotificationCommands;

/// <summary>
/// Command to create a new notification in the database.
/// </summary>
/// <param name="Text">Notification text content.</param>
/// <param name="AgentName">Agent name (e.g., "opencode", "claude", "gemini").</param>
/// <param name="IssueIds">Optional GitHub issue IDs to associate with this notification.</param>
public record CreateNotificationCommand(
    string Text,
    string AgentName,
    IReadOnlyList<int>? IssueIds = null
) : ICommand<int>;
