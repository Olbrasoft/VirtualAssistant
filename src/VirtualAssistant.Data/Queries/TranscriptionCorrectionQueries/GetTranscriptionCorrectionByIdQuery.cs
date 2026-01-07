using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.TranscriptionCorrectionQueries;

/// <summary>
/// Query to get a correction by its unique identifier.
/// </summary>
/// <param name="Id">The correction ID. Must be greater than 0.</param>
public record GetTranscriptionCorrectionByIdQuery(int Id)
    : IQuery<TranscriptionCorrection?>;
