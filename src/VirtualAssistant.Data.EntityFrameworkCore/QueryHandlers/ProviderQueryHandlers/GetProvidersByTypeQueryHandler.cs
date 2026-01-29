using Microsoft.EntityFrameworkCore;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Data.Queries.ProviderQueries;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.ProviderQueryHandlers;

/// <summary>
/// Handler for GetProvidersByTypeQuery.
/// Returns all enabled providers of a specific type, ordered by priority.
/// </summary>
public class GetProvidersByTypeQueryHandler(VirtualAssistantDbContext context)
    : IQueryHandler<GetProvidersByTypeQuery, IReadOnlyList<Provider>>
{
    public async Task<IReadOnlyList<Provider>> HandleAsync(
        GetProvidersByTypeQuery query,
        CancellationToken token = default)
    {
        return await context.Providers
            .Where(p => p.Type == query.Type && p.Enabled)
            .OrderBy(p => p.Priority)
            .AsNoTracking()
            .ToListAsync(token);
    }
}
