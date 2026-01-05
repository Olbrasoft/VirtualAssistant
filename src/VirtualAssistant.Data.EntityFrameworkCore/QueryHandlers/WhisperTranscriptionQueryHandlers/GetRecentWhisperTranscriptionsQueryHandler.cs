using Olbrasoft.VirtualAssistant.Data.Queries.WhisperTranscriptionQueries;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.WhisperTranscriptionQueryHandlers;

/// <summary>
/// Handler for GetRecentWhisperTranscriptionsQuery.
/// Returns the most recent Whisper transcriptions.
/// </summary>
public class GetRecentWhisperTranscriptionsQueryHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbQueryHandler<WhisperTranscription, GetRecentWhisperTranscriptionsQuery, IReadOnlyList<WhisperTranscription>>(context)
{
    protected override async Task<IReadOnlyList<WhisperTranscription>> GetResultToHandleAsync(GetRecentWhisperTranscriptionsQuery query, CancellationToken token)
    {
        return await Context.Set<WhisperTranscription>()
            .OrderByDescending(w => w.CreatedAt)
            .Take(query.Count)
            .ToListAsync(token);
    }
}
