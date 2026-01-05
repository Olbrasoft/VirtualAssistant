using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.VoiceTranscriptionQueries;

/// <summary>
/// Query to get the most recent voice transcriptions.
/// </summary>
/// <param name="Count">Maximum number of transcriptions to return (default: 50). Must be greater than 0.</param>
public record GetRecentVoiceTranscriptionsQuery(int Count = 50)
    : IQuery<IReadOnlyList<VoiceTranscription>>;
