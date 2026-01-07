using Olbrasoft.VirtualAssistant.Data.Enums;
using Olbrasoft.VirtualAssistant.Data.Queries.NotificationQueries;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.NotificationQueryHandlers;

/// <summary>
/// Handler for GetNewNotificationsQuery.
/// Returns all notifications with NewlyReceived status.
/// </summary>
public class GetNewNotificationsQueryHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbQueryHandler<Notification, GetNewNotificationsQuery, IReadOnlyList<Notification>>(context)
{
    protected override async Task<IReadOnlyList<Notification>> GetResultToHandleAsync(GetNewNotificationsQuery query, CancellationToken token)
    {
        return await Where(n => n.NotificationStatusId == (int)NotificationStatusEnum.NewlyReceived)
            .AsNoTracking()
            .OrderBy(n => n.CreatedAt)
            .ToListAsync(token);
    }
}
