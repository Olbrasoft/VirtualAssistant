using Microsoft.EntityFrameworkCore;
using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Data.Queries.PromptQueries;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.PromptQueryHandlers;

/// <summary>
/// Handler for GetPromptByAppIdPatternQuery.
/// Finds a prompt where the active application name contains the prompt's AppIdPattern.
/// Returns NULL if no match found.
/// </summary>
public class GetPromptByAppIdPatternQueryHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbQueryHandler<Prompt, GetPromptByAppIdPatternQuery, Prompt?>(context)
{
    protected override async Task<Prompt?> GetResultToHandleAsync(GetPromptByAppIdPatternQuery query, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query.ActiveApplication, nameof(query.ActiveApplication));

        var appLower = query.ActiveApplication.ToLowerInvariant();

        // Get all prompts except Default (AppIdPattern = "*")
        var prompts = await Context.Prompts
            .Where(p => p.AppIdPattern != "*")
            .ToListAsync(token);

        // Find first match where ActiveApplication contains AppIdPattern
        // Example: activeApp = "google-chrome-stable" matches AppIdPattern = "chrome"
        return prompts.FirstOrDefault(p => appLower.Contains(p.AppIdPattern.ToLowerInvariant()));
    }
}
