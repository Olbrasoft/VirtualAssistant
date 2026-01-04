using Microsoft.EntityFrameworkCore;
using Olbrasoft.VirtualAssistant.Data.Queries.TranscriptionCorrectionQueries;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.TranscriptionCorrectionQueryHandlers;

/// <summary>
/// Handler for GetActiveTranscriptionCorrectionsQuery.
/// Returns all active corrections ordered by priority (highest first).
/// Used by TextFilter for in-memory caching.
/// </summary>
public class GetActiveTranscriptionCorrectionsQueryHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbQueryHandler<TranscriptionCorrection, GetActiveTranscriptionCorrectionsQuery, IReadOnlyList<TranscriptionCorrection>>(context)
{
    protected override async Task<IReadOnlyList<TranscriptionCorrection>> GetResultToHandleAsync(GetActiveTranscriptionCorrectionsQuery query, CancellationToken token)
    {
        return await Where(c => c.IsActive)
            .OrderByDescending(c => c.Priority)
            .ToListAsync(token);
    }
}
