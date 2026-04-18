using Olbrasoft.VirtualAssistant.Data.Queries.PromptQueries;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.PromptQueryHandlers;

/// <summary>
/// Handler for GetPromptByFileNameQuery.
/// Returns a prompt by its file name (e.g., "ClaudeCodeCorrection").
/// </summary>
public class GetPromptByFileNameQueryHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbQueryHandler<Prompt, GetPromptByFileNameQuery, Prompt?>(context)
{
    protected override async Task<Prompt?> GetResultToHandleAsync(GetPromptByFileNameQuery query, CancellationToken token)
    {
        return await Context.Prompts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PromptFileName == query.PromptFileName, token);
    }
}
