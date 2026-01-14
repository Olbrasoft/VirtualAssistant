using Olbrasoft.Data.Paging;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.LlmCorrectionQueries;

public record GetLlmCorrectionsWithPromptsQuery(
    string? SearchString,
    DateTime? StartDate,
    DateTime? EndDate,
    int PageIndex = 1,
    int PageSize = 20) : IQuery<IPagedEnumerable<LlmCorrection>>;
