using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.VoiceTranscriptionQueries;

/// <summary>
/// Query to search for voice transcriptions containing the specified text.
/// </summary>
/// <param name="SearchQuery">The search query. Must not be null or empty.</param>
public record SearchVoiceTranscriptionsQuery(string SearchQuery)
    : IQuery<IReadOnlyList<VoiceTranscription>>;
