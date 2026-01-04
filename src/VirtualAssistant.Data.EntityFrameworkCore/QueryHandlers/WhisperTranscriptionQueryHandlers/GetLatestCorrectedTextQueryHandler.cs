using Microsoft.EntityFrameworkCore;
using Olbrasoft.VirtualAssistant.Data.Queries.WhisperTranscriptionQueries;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.WhisperTranscriptionQueryHandlers;

/// <summary>
/// Handler for GetLatestCorrectedTextQuery.
/// Returns the latest corrected text (LLM correction if available, otherwise Whisper text).
/// </summary>
public class GetLatestCorrectedTextQueryHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbQueryHandler<WhisperTranscription, GetLatestCorrectedTextQuery, string?>(context)
{
    protected override async Task<string?> GetResultToHandleAsync(GetLatestCorrectedTextQuery query, CancellationToken token)
    {
        var latestTranscription = await Context.Set<WhisperTranscription>()
            .OrderByDescending(w => w.CreatedAt)
            .FirstOrDefaultAsync(token);

        if (latestTranscription == null)
            return null;

        // Check if there's an LLM correction for this transcription
        var correction = await Context.LlmCorrections
            .Where(c => c.WhisperTranscriptionId == latestTranscription.Id)
            .Select(c => c.CorrectedText)
            .FirstOrDefaultAsync(token);

        return correction ?? latestTranscription.TranscribedText;
    }
}
