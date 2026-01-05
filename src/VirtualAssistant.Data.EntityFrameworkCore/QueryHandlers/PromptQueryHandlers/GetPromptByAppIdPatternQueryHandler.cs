using Microsoft.EntityFrameworkCore;
using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Data.Queries.PromptQueries;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.PromptQueryHandlers;

/// <summary>
/// Handler for GetPromptByAppIdPatternQuery.
/// Finds a prompt where the active window title contains the prompt's AppIdPattern.
/// Tests against window title (e.g., "Claude Code", "Ferdium - WhatsApp", "OpenCode").
/// Returns NULL if no match found.
/// </summary>
public class GetPromptByAppIdPatternQueryHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbQueryHandler<Prompt, GetPromptByAppIdPatternQuery, Prompt?>(context)
{
    protected override async Task<Prompt?> GetResultToHandleAsync(GetPromptByAppIdPatternQuery query, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query.ActiveWindowTitle, nameof(query.ActiveWindowTitle));

        var titleLower = query.ActiveWindowTitle.ToLowerInvariant();

        // Find first match where ActiveWindowTitle contains AppIdPattern (case-insensitive, database-side filtering)
        // Example: windowTitle = "Claude Code - file.cs" matches AppIdPattern = "code"
        // Example: windowTitle = "Ferdium - WhatsApp - (1) WhatsApp" matches AppIdPattern = "ferdium"
        // Uses EF.Functions.Like for database-side pattern matching to avoid loading all prompts into memory
        return await Context.Prompts
            .Where(p => p.AppIdPattern != "*")
            .Where(p => EF.Functions.Like(titleLower, "%" + p.AppIdPattern.ToLower() + "%"))
            .FirstOrDefaultAsync(token);
    }
}
