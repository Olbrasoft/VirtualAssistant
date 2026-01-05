using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.WhisperTranscriptionQueries;

/// <summary>
/// Query to search transcriptions by text content (case-insensitive partial match).
/// </summary>
/// <param name="SearchQuery">Search query text. Must not be null or empty.</param>
public record SearchWhisperTranscriptionsQuery(string SearchQuery)
    : IQuery<IReadOnlyList<WhisperTranscription>>;
