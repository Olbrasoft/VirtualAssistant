using Olbrasoft.VirtualAssistant.Data.Queries.TranscriptionCorrectionQueries;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.TranscriptionCorrectionQueryHandlers;

/// <summary>
/// Handler for GetTranscriptionCorrectionByIdQuery.
/// Returns a correction by its unique identifier.
/// </summary>
public class GetTranscriptionCorrectionByIdQueryHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbQueryHandler<TranscriptionCorrection, GetTranscriptionCorrectionByIdQuery, TranscriptionCorrection?>(context)
{
    protected override async Task<TranscriptionCorrection?> GetResultToHandleAsync(GetTranscriptionCorrectionByIdQuery query, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(query.Id, nameof(query.Id));

        return await Where(c => c.Id == query.Id)
            .FirstOrDefaultAsync(token);
    }
}
