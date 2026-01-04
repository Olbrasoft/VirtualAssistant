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
        return await Where(w => EF.Functions.ILike(w.TranscribedText, $"%{query.SearchQuery}%"))
            .OrderByDescending(w => w.Id)
            .ToListAsync(token);
    }
}
