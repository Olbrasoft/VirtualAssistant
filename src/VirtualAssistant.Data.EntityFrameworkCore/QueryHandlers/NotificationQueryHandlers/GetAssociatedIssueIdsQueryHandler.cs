using Olbrasoft.VirtualAssistant.Data.Queries.NotificationQueries;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.NotificationQueryHandlers;

/// <summary>
/// Handler for GetAssociatedIssueIdsQuery.
/// Returns all GitHub issue IDs associated with the given notifications.
/// </summary>
public class GetAssociatedIssueIdsQueryHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbQueryHandler<NotificationGitHubIssue, GetAssociatedIssueIdsQuery, IReadOnlyList<int>>(context)
{
    protected override async Task<IReadOnlyList<int>> GetResultToHandleAsync(GetAssociatedIssueIdsQuery query, CancellationToken token)
    {
        if (query.NotificationIds.Count == 0)
        {
            return [];
        }

        return await Where(ngi => query.NotificationIds.Contains(ngi.NotificationId))
            .Select(ngi => ngi.GitHubIssueId)
            .Distinct()
            .ToListAsync(token);
    }
}
