using Olbrasoft.Data.Cqrs;

namespace Olbrasoft.VirtualAssistant.Data.Queries.WhisperTranscriptionQueries;

/// <summary>
/// Query to get the latest corrected text (LLM correction if available, otherwise Whisper text).
/// </summary>
public class GetLatestCorrectedTextQuery : BaseQuery<string?>
{
    public GetLatestCorrectedTextQuery(IQueryProcessor processor) : base(processor) { }
    public GetLatestCorrectedTextQuery(IMediator mediator) : base(mediator) { }
}
