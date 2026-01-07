using Olbrasoft.VirtualAssistant.Data.Commands.NotificationCommands;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.CommandHandlers.NotificationCommandHandlers;

/// <summary>
/// Handler for UpdateNotificationStatusCommand.
/// Updates the status of a single notification.
/// </summary>
public class UpdateNotificationStatusCommandHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbCommandHandler<UpdateNotificationStatusCommand, Notification>(context)
{
    protected override async Task<bool> GetResultToHandleAsync(UpdateNotificationStatusCommand command, CancellationToken token)
    {
        var notification = await Context.Notifications.FindAsync([command.NotificationId], token);
        if (notification == null)
        {
            return false;
        }

        notification.NotificationStatusId = (int)command.NewStatus;
        await Context.SaveChangesAsync(token);
        return true;
    }
}
