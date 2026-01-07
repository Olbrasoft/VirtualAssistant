using Olbrasoft.VirtualAssistant.Data.Enums;

namespace Olbrasoft.VirtualAssistant.Data.Commands.NotificationCommands;

/// <summary>
/// Command to update the status of multiple notifications.
/// </summary>
/// <param name="NotificationIds">IDs of notifications to update.</param>
/// <param name="NewStatus">New status to set.</param>
public record UpdateNotificationStatusBatchCommand(
    IReadOnlyList<int> NotificationIds,
    NotificationStatusEnum NewStatus
) : ICommand<int>;
