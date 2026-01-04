using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.VoiceTranscriptionQueries;

/// <summary>
/// Query to search for voice transcriptions containing the specified text.
/// </summary>
public class SearchVoiceTranscriptionsQuery : BaseQuery<IReadOnlyList<VoiceTranscription>>
{
    public SearchVoiceTranscriptionsQuery(IQueryProcessor processor) : base(processor) { }
    public SearchVoiceTranscriptionsQuery(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// The search query. Must not be null or empty.
    /// </summary>
    public string SearchQuery { get; set; } = string.Empty;
}
