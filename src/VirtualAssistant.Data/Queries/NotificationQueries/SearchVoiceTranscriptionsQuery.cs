using Olbrasoft.Data.Cqrs;
using VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.NotificationQueries;

/// <summary>
/// Query to search for voice transcriptions containing the specified text.
/// </summary>
/// <param name="SearchQuery">The search query.</param>
public record SearchVoiceTranscriptionsQuery(string SearchQuery) : IQuery<IReadOnlyList<VoiceTranscription>>;
