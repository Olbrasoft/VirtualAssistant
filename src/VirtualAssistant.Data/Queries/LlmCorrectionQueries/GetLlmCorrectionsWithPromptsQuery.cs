using Olbrasoft.Data.Paging;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.LlmCorrectionQueries;

/// <summary>
/// Query to retrieve a paginated list of LLM corrections including prompts and original transcriptions.
/// Supports filtering by text search and date range.
/// </summary>
/// <param name="SearchString">Text to search for in both correction and original transcription.</param>
/// <param name="StartDate">Filter corrections created on or after this date.</param>
/// <param name="EndDate">Filter corrections created before the end of this date.</param>
/// <param name="PageIndex">The 1-based index of the page to retrieve.</param>
/// <param name="PageSize">The number of items per page.</param>
public record GetLlmCorrectionsWithPromptsQuery(
    string? SearchString,
    DateTime? StartDate,
    DateTime? EndDate,
    int PageIndex = 1,
    int PageSize = 20) : IQuery<IPagedEnumerable<LlmCorrection>>;
