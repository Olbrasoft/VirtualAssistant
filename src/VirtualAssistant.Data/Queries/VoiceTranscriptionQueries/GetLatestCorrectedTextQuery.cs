namespace Olbrasoft.VirtualAssistant.Data.Queries.VoiceTranscriptionQueries;

/// <summary>
/// Query to get the latest corrected text (LLM correction if available, otherwise transcribed text).
/// </summary>
public record GetLatestCorrectedTextQuery() : IQuery<string?>;
