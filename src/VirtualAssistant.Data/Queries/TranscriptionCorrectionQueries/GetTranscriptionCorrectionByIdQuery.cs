using Olbrasoft.Data.Cqrs;
using VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.TranscriptionCorrectionQueries;

/// <summary>
/// Query to get a correction by its unique identifier.
/// </summary>
public class GetTranscriptionCorrectionByIdQuery : BaseQuery<TranscriptionCorrection?>
{
    public GetTranscriptionCorrectionByIdQuery(IQueryProcessor processor) : base(processor) { }
    public GetTranscriptionCorrectionByIdQuery(IMediator mediator) : base(mediator) { }

    /// <summary>
    /// The correction ID. Must be greater than 0.
    /// </summary>
    public int Id { get; set; } = -1;
}
