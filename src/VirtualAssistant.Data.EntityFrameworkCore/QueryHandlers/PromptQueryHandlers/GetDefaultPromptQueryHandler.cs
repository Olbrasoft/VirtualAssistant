using Olbrasoft.VirtualAssistant.Data.Queries.PromptQueries;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.PromptQueryHandlers;

/// <summary>
/// Handler for GetDefaultPromptQuery.
/// Returns the Default prompt (AppIdPattern = "*") used as fallback.
/// </summary>
public class GetDefaultPromptQueryHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbQueryHandler<Prompt, GetDefaultPromptQuery, Prompt>(context)
{
    protected override async Task<Prompt> GetResultToHandleAsync(GetDefaultPromptQuery query, CancellationToken token)
    {
        var defaultPrompt = await Context.Prompts
            .FirstOrDefaultAsync(p => p.AppIdPattern == "*", token);

        if (defaultPrompt == null)
        {
            throw new InvalidOperationException("Default prompt (AppIdPattern = '*') not found in database. This is a critical configuration error.");
        }

        return defaultPrompt;
    }
}
