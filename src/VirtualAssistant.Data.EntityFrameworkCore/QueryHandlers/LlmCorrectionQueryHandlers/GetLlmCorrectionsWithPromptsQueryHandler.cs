using Olbrasoft.Data.Paging;
using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Data.Queries.LlmCorrectionQueries;

namespace Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore.QueryHandlers.LlmCorrectionQueryHandlers;

/// <summary>
/// Handler for <see cref="GetLlmCorrectionsWithPromptsQuery"/>.
/// Retrieves LlmCorrections from database with filtering and pagination.
/// </summary>
public class GetLlmCorrectionsWithPromptsQueryHandler(VirtualAssistantDbContext context)
    : VirtualAssistantDbQueryHandler<LlmCorrection, GetLlmCorrectionsWithPromptsQuery, IPagedEnumerable<LlmCorrection>>(context)
{
    protected override async Task<IPagedEnumerable<LlmCorrection>> GetResultToHandleAsync(GetLlmCorrectionsWithPromptsQuery query, CancellationToken token)
    {
        var dbSet = Context.Set<LlmCorrection>()
            .Include(c => c.WhisperTranscription)
            .Include(c => c.Prompt)
            .AsNoTracking();

        IQueryable<LlmCorrection> q = dbSet;

        if (!string.IsNullOrWhiteSpace(query.SearchString))
        {
            var search = query.SearchString.Trim();
            q = q.Where(c => 
                c.CorrectedText.Contains(search) || 
                c.WhisperTranscription.TranscribedText.Contains(search));
        }

        if (query.StartDate.HasValue)
        {
            var utcStart = query.StartDate.Value.ToUniversalTime();
            q = q.Where(c => c.CreatedAt >= utcStart);
        }

        if (query.EndDate.HasValue)
        {
             // End of the day in UTC effectively
            var utcEnd = query.EndDate.Value.ToUniversalTime().AddDays(1);
            q = q.Where(c => c.CreatedAt < utcEnd);
        }

        var totalCount = await q.CountAsync(token);

        // Ensure PageIndex is valid
        var pageIndex = query.PageIndex < 1 ? 1 : query.PageIndex;
        
        var items = await q
            .OrderByDescending(c => c.CreatedAt)
            .Skip((pageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(token);

        return new PagedResult<LlmCorrection>(items, totalCount);
    }
}
