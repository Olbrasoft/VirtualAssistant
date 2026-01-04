using Olbrasoft.Data.Cqrs;
using VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.WhisperTranscriptionQueries;

/// <summary>
/// Query to get the most recent Whisper transcriptions.
/// </summary>
public class GetRecentWhisperTranscriptionsQuery : BaseQuery<IReadOnlyList<WhisperTranscription>>
{
    public GetRecentWhisperTranscriptionsQuery(IQueryProcessor processor) : base(processor) { }
    public GetRecentWhisperTranscriptionsQuery(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// Number of recent transcriptions to retrieve (default: 50). Must be greater than 0.
    /// </summary>
    public int Count { get; set; } = 50;
}
