using Microsoft.EntityFrameworkCore;
using Olbrasoft.VirtualAssistant.Data.Queries.TranscriptionCorrectionQueries;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.TranscriptionCorrectionQueryHandlers;

/// <summary>
/// Handler for GetTranscriptionCorrectionByIdQuery.
/// Returns a correction by its unique identifier.
/// </summary>
public class GetTranscriptionCorrectionByIdQueryHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbQueryHandler<TranscriptionCorrection, GetTranscriptionCorrectionByIdQuery, TranscriptionCorrection?>(context)
{
    protected override async Task<TranscriptionCorrection?> GetResultToHandleAsync(GetTranscriptionCorrectionByIdQuery query, CancellationToken token)
    {
        return await Where(c => c.Id == query.Id)
            .FirstOrDefaultAsync(token);
    }
}
