namespace Olbrasoft.VirtualAssistant.Data.Queries.WhisperTranscriptionQueries;

/// <summary>
/// Query to get the latest corrected text (LLM correction if available, otherwise Whisper text).
/// </summary>
public record GetLatestCorrectedTextQuery() : IQuery<string?>;
