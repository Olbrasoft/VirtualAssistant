using Olbrasoft.VirtualAssistant.Data.Enums;

namespace Olbrasoft.VirtualAssistant.Data.Commands.NotificationCommands;

/// <summary>
/// Command to update the status of a single notification.
/// </summary>
/// <param name="NotificationId">ID of the notification to update.</param>
/// <param name="NewStatus">New status to set.</param>
public record UpdateNotificationStatusCommand(
    int NotificationId,
    NotificationStatusEnum NewStatus
) : ICommand<bool>;
