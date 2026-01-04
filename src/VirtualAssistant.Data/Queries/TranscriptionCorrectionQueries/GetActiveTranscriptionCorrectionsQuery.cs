using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.TranscriptionCorrectionQueries;

/// <summary>
/// Query to get all active corrections ordered by priority (highest first).
/// Used by TextFilter for in-memory caching.
/// </summary>
public class GetActiveTranscriptionCorrectionsQuery : BaseQuery<IReadOnlyList<TranscriptionCorrection>>
{
    public GetActiveTranscriptionCorrectionsQuery(IQueryProcessor processor) : base(processor) { }
    public GetActiveTranscriptionCorrectionsQuery(IMediator mediator) : base(mediator) { }
}
