using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.NotificationQueries;

/// <summary>
/// Query to get all notifications with NewlyReceived status.
/// </summary>
public record GetNewNotificationsQuery() : IQuery<IReadOnlyList<Notification>>;
