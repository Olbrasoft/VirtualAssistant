using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Queries.WhisperTranscriptionQueries;

/// <summary>
/// Query to get the most recent Whisper transcriptions.
/// </summary>
/// <param name="Count">Number of recent transcriptions to retrieve (default: 50). Must be greater than 0.</param>
public record GetRecentWhisperTranscriptionsQuery(int Count = 50)
    : IQuery<IReadOnlyList<WhisperTranscription>>;
