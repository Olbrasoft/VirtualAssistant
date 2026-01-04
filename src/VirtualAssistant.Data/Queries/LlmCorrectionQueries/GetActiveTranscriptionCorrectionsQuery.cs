using Olbrasoft.Data.Cqrs;
using VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.LlmCorrectionQueries;

/// <summary>
/// Query to get all active corrections ordered by priority (highest first).
/// Used by TextFilter for in-memory caching.
/// </summary>
public record GetActiveTranscriptionCorrectionsQuery : IQuery<IReadOnlyList<TranscriptionCorrection>>;
