using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.VoiceTranscriptionQueries;

/// <summary>
/// Query to get the most recent voice transcriptions.
/// </summary>
public class GetRecentVoiceTranscriptionsQuery : BaseQuery<IReadOnlyList<VoiceTranscription>>
{
    public GetRecentVoiceTranscriptionsQuery(IQueryProcessor processor) : base(processor) { }
    public GetRecentVoiceTranscriptionsQuery(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// Maximum number of transcriptions to return (default: 50). Must be greater than 0.
    /// </summary>
    public int Count { get; set; } = 50;
}
