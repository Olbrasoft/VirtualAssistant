using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Data.Queries.LlmModelQueries;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.LlmModelQueryHandlers;

/// <summary>
/// Handler for GetLlmModelByIdentifierQuery.
/// Returns the LlmModel matching the given model identifier, or null if not found.
/// </summary>
public class GetLlmModelByIdentifierQueryHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbQueryHandler<LlmModel, GetLlmModelByIdentifierQuery, LlmModel?>(context)
{
    protected override async Task<LlmModel?> GetResultToHandleAsync(GetLlmModelByIdentifierQuery query, CancellationToken token)
    {
        return await Context.LlmModels
            .FirstOrDefaultAsync(m => m.ModelIdentifier == query.ModelIdentifier && m.IsActive, token);
    }
}
