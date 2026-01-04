using Microsoft.EntityFrameworkCore;
using Olbrasoft.VirtualAssistant.Data.Queries.WhisperTranscriptionQueries;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.WhisperTranscriptionQueryHandlers;

/// <summary>
/// Handler for SearchWhisperTranscriptionsQuery.
/// Searches transcriptions by text content (case-insensitive partial match).
/// </summary>
public class SearchWhisperTranscriptionsQueryHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbQueryHandler<WhisperTranscription, SearchWhisperTranscriptionsQuery, IReadOnlyList<WhisperTranscription>>(context)
{
    protected override async Task<IReadOnlyList<WhisperTranscription>> GetResultToHandleAsync(SearchWhisperTranscriptionsQuery query, CancellationToken token)
    {
        // If search query is empty, return recent transcriptions
        if (string.IsNullOrWhiteSpace(query.SearchQuery))
        {
            return await Context.Set<WhisperTranscription>()
                .OrderByDescending(w => w.CreatedAt)
                .Take(100)
                .ToListAsync(token);
        }

        var escapedSearch = EscapeLikePattern(query.SearchQuery);
        var searchPattern = $"%{escapedSearch}%";

        return await Where(w => EF.Functions.ILike(w.TranscribedText, searchPattern))
            .OrderByDescending(w => w.CreatedAt)
            .Take(100)
            .ToListAsync(token);
    }
}
