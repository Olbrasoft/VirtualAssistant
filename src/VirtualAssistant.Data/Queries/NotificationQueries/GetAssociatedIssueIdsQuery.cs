using Olbrasoft.Data.Cqrs;

namespace Olbrasoft.VirtualAssistant.Data.Queries.NotificationQueries;

/// <summary>
/// Query to get all GitHub issue IDs associated with the given notifications.
/// </summary>
/// <param name="NotificationIds">IDs of notifications to get issues for.</param>
public record GetAssociatedIssueIdsQuery(
    IReadOnlyList<int> NotificationIds
) : IQuery<IReadOnlyList<int>>;
