using Microsoft.EntityFrameworkCore;
using Olbrasoft.VirtualAssistant.Data.Queries.WhisperTranscriptionQueries;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.WhisperTranscriptionQueryHandlers;

/// <summary>
/// Handler for GetRecentWhisperTranscriptionsQuery.
/// Returns the most recent Whisper transcriptions.
/// </summary>
public class GetRecentWhisperTranscriptionsQueryHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbQueryHandler<WhisperTranscription, GetRecentWhisperTranscriptionsQuery, IReadOnlyList<WhisperTranscription>>(context)
{
    protected override async Task<IReadOnlyList<WhisperTranscription>> GetResultToHandleAsync(GetRecentWhisperTranscriptionsQuery query, CancellationToken token)
    {
        return await Where(w => true)
            .OrderByDescending(w => w.Id)
            .Take(query.Count)
            .ToListAsync(token);
    }
}
