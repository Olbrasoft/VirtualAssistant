using Olbrasoft.Data.Cqrs;
using VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.WhisperTranscriptionQueries;

/// <summary>
/// Query to search transcriptions by text content (case-insensitive partial match).
/// </summary>
public class SearchWhisperTranscriptionsQuery : BaseQuery<IReadOnlyList<WhisperTranscription>>
{
    public SearchWhisperTranscriptionsQuery(IQueryProcessor processor) : base(processor) { }
    public SearchWhisperTranscriptionsQuery(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// Search query text. Must not be null or empty.
    /// </summary>
    public string SearchQuery { get; set; } = string.Empty;
}
