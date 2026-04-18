using Olbrasoft.VirtualAssistant.Data.Queries.VoiceTranscriptionQueries;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.VoiceTranscriptionQueryHandlers;

/// <summary>
/// Handler for SearchVoiceTranscriptionsQuery.
/// Searches transcriptions by text content (case-insensitive partial match).
/// </summary>
public class SearchVoiceTranscriptionsQueryHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbQueryHandler<VoiceTranscription, SearchVoiceTranscriptionsQuery, IReadOnlyList<VoiceTranscription>>(context)
{
    protected override async Task<IReadOnlyList<VoiceTranscription>> GetResultToHandleAsync(SearchVoiceTranscriptionsQuery query, CancellationToken token)
    {
        // If search query is empty, return recent transcriptions
        if (string.IsNullOrWhiteSpace(query.SearchQuery))
        {
            return await Context.Set<VoiceTranscription>()
                .AsNoTracking()
                .Include(v => v.Provider)
                .OrderByDescending(v => v.CreatedAt)
                .Take(100)
                .ToListAsync(token);
        }

        var escapedSearch = EscapeLikePattern(query.SearchQuery);
        var searchPattern = $"%{escapedSearch}%";

        return await Where(v => EF.Functions.ILike(v.TranscribedText, searchPattern))
            .AsNoTracking()
            .Include(v => v.Provider)
            .OrderByDescending(v => v.CreatedAt)
            .Take(100)
            .ToListAsync(token);
    }
}
