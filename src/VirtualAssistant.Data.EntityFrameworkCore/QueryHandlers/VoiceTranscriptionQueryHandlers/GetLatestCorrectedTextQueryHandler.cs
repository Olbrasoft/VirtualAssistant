using Olbrasoft.VirtualAssistant.Data.Queries.VoiceTranscriptionQueries;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.VoiceTranscriptionQueryHandlers;

/// <summary>
/// Handler for GetLatestCorrectedTextQuery.
/// Returns the latest corrected text (LLM correction if available, otherwise transcribed text).
/// </summary>
public class GetLatestCorrectedTextQueryHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbQueryHandler<VoiceTranscription, GetLatestCorrectedTextQuery, string?>(context)
{
    protected override async Task<string?> GetResultToHandleAsync(GetLatestCorrectedTextQuery query, CancellationToken token)
    {
        var latestTranscription = await Context.Set<VoiceTranscription>()
            .AsNoTracking()
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(token);

        if (latestTranscription == null)
            return null;

        // Check if there's an LLM correction for this transcription
        var correction = await Context.LlmCorrections
            .AsNoTracking()
            .Where(c => c.VoiceTranscriptionId == latestTranscription.Id)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => c.CorrectedText)
            .FirstOrDefaultAsync(token);

        return correction ?? latestTranscription.TranscribedText;
    }
}
