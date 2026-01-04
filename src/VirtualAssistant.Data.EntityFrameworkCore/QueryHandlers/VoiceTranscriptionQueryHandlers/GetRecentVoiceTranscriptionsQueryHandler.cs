using Microsoft.EntityFrameworkCore;
using Olbrasoft.VirtualAssistant.Data.Queries.VoiceTranscriptionQueries;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.VoiceTranscriptionQueryHandlers;

/// <summary>
/// Handler for GetRecentVoiceTranscriptionsQuery.
/// Returns the most recent voice transcriptions.
/// </summary>
public class GetRecentVoiceTranscriptionsQueryHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbQueryHandler<VoiceTranscription, GetRecentVoiceTranscriptionsQuery, IReadOnlyList<VoiceTranscription>>(context)
{
    protected override async Task<IReadOnlyList<VoiceTranscription>> GetResultToHandleAsync(GetRecentVoiceTranscriptionsQuery query, CancellationToken token)
    {
        return await Context.Set<VoiceTranscription>()
            .OrderByDescending(v => v.CreatedAt)
            .Take(query.Count)
            .ToListAsync(token);
    }
}
