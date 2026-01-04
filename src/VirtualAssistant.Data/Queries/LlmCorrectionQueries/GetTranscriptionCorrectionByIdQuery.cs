using Olbrasoft.Data.Cqrs;
using VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.LlmCorrectionQueries;

/// <summary>
/// Query to get a correction by its unique identifier.
/// </summary>
/// <param name="Id">The correction ID.</param>
public record GetTranscriptionCorrectionByIdQuery(int Id) : IQuery<TranscriptionCorrection?>;
