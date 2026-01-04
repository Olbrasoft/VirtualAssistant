using Microsoft.EntityFrameworkCore;
using Olbrasoft.VirtualAssistant.Data.Queries.VoiceTranscriptionQueries;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.VoiceTranscriptionQueryHandlers;

/// <summary>
/// Handler for SearchVoiceTranscriptionsQuery.
/// Searches for voice transcriptions containing the specified text.
/// </summary>
public class SearchVoiceTranscriptionsQueryHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbQueryHandler<VoiceTranscription, SearchVoiceTranscriptionsQuery, IReadOnlyList<VoiceTranscription>>(context)
{
    protected override async Task<IReadOnlyList<VoiceTranscription>> GetResultToHandleAsync(SearchVoiceTranscriptionsQuery query, CancellationToken token)
    {
        var escapedSearch = EscapeLikePattern(query.SearchQuery);
        var searchPattern = $"%{escapedSearch}%";

        return await Where(v => EF.Functions.ILike(v.TranscribedText, searchPattern))
            .OrderByDescending(v => v.CreatedAt)
            .Take(100)
            .ToListAsync(token);
    }
}
