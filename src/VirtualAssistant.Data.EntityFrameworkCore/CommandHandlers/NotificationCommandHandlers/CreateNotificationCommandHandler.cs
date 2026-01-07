using Olbrasoft.VirtualAssistant.Data.Commands.NotificationCommands;
using Olbrasoft.VirtualAssistant.Data.Enums;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers.NotificationCommandHandlers;

/// <summary>
/// Handler for CreateNotificationCommand.
/// Creates a new notification in the database.
/// </summary>
public class CreateNotificationCommandHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbCommandHandler<CreateNotificationCommand, Notification, int>(context)
{
    protected override async Task<int> GetResultToHandleAsync(CreateNotificationCommand command, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Text, nameof(command.Text));
        ArgumentException.ThrowIfNullOrWhiteSpace(command.AgentName, nameof(command.AgentName));

        var agentType = MapAgentNameToType(command.AgentName);

        var notification = new Notification
        {
            Text = command.Text,
            AgentId = (int)agentType,
            CreatedAt = DateTime.UtcNow,
            NotificationStatusId = (int)NotificationStatusEnum.NewlyReceived
        };

        Context.Notifications.Add(notification);

        // Add issue links using navigation property - EF Core handles FK assignment
        if (command.IssueIds is { Count: > 0 })
        {
            foreach (var issueId in command.IssueIds.Distinct())
            {
                Context.NotificationGitHubIssues.Add(new NotificationGitHubIssue
                {
                    Notification = notification,  // Use navigation property instead of ID
                    GitHubIssueId = issueId
                });
            }
        }

        // Single SaveChanges - EF Core sets notification.Id and propagates to NotificationGitHubIssue.NotificationId
        await Context.SaveChangesAsync(token);

        return notification.Id;
    }

    private static AgentType MapAgentNameToType(string agentName)
    {
        var normalized = agentName.ToLowerInvariant().Trim();

        return normalized switch
        {
            "opencode" => AgentType.OpenCode,
            "claude" or "claude-code" => AgentType.ClaudeCode,
            "gemini" => AgentType.Gemini,
            _ => throw new ArgumentException(
                $"Invalid agent name '{agentName}'. Allowed values: opencode, claude, claude-code, gemini",
                nameof(agentName))
        };
    }
}
