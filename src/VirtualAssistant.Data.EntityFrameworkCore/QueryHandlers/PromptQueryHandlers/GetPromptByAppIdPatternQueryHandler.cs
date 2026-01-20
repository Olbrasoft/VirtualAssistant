using Olbrasoft.VirtualAssistant.Data.Queries.PromptQueries;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.PromptQueryHandlers;

/// <summary>
/// Handler for GetPromptByAppIdPatternQuery.
/// Finds a prompt by matching against application pattern (priority) or window title pattern.
/// Priority: 1) ApplicationPattern matches ActiveApplication (e.g., "antigravity.desktop")
///           2) AppIdPattern matches ActiveWindowTitle (e.g., "Claude Code", "OC |")
/// Returns NULL if no match found.
/// </summary>
public class GetPromptByAppIdPatternQueryHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbQueryHandler<Prompt, GetPromptByAppIdPatternQuery, Prompt?>(context)
{
    protected override async Task<Prompt?> GetResultToHandleAsync(GetPromptByAppIdPatternQuery query, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query.ActiveWindowTitle, nameof(query.ActiveWindowTitle));

        // Priority 1: Match by ApplicationPattern (desktop file name)
        // Example: ActiveApplication = "antigravity.desktop" matches ApplicationPattern = "antigravity"
        if (!string.IsNullOrWhiteSpace(query.ActiveApplication))
        {
            var appMatch = await Context.Prompts
                .Where(p => p.ApplicationPattern != null && p.ApplicationPattern != "")
                .Where(p => EF.Functions.Like(query.ActiveApplication, "%" + p.ApplicationPattern + "%"))
                .FirstOrDefaultAsync(token);

            if (appMatch != null)
                return appMatch;
        }

        // Priority 2: Match by AppIdPattern (window title)
        // Example: windowTitle = "Claude Code - file.cs" matches AppIdPattern = "Claude Code"
        // Example: windowTitle = "OC | Create issues" matches AppIdPattern = "OC |"
        return await Context.Prompts
            .Where(p => p.AppIdPattern != "*")
            .Where(p => EF.Functions.Like(query.ActiveWindowTitle, "%" + p.AppIdPattern + "%"))
            .FirstOrDefaultAsync(token);
    }
}
