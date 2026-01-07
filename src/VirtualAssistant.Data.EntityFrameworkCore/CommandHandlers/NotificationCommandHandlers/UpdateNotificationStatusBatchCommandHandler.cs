using Olbrasoft.VirtualAssistant.Data.Commands.NotificationCommands;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers.NotificationCommandHandlers;

/// <summary>
/// Handler for UpdateNotificationStatusBatchCommand.
/// Updates the status of multiple notifications.
/// </summary>
public class UpdateNotificationStatusBatchCommandHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbCommandHandler<UpdateNotificationStatusBatchCommand, Notification, int>(context)
{
    protected override async Task<int> GetResultToHandleAsync(UpdateNotificationStatusBatchCommand command, CancellationToken token)
    {
        if (command.NotificationIds.Count == 0)
        {
            return 0;
        }

        var notifications = await Context.Notifications
            .Where(n => command.NotificationIds.Contains(n.Id))
            .ToListAsync(token);

        foreach (var notification in notifications)
        {
            notification.NotificationStatusId = (int)command.NewStatus;
        }

        await Context.SaveChangesAsync(token);
        return notifications.Count;
    }
}
