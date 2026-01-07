using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Voice.Filters;

/// <summary>
/// Repository abstraction for accessing transcription corrections.
/// Follows OCP - allows different data sources without modifying filter strategy.
/// </summary>
public interface ITranscriptionCorrectionRepository
{
    /// <summary>
    /// Get all active corrections ordered by priority (highest first).
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of active corrections</returns>
    Task<IReadOnlyList<TranscriptionCorrection>> GetActiveCorrectionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Track that a correction was applied.
    /// </summary>
    /// <param name="correctionId">ID of the applied correction</param>
    /// <param name="ct">Cancellation token</param>
    Task TrackUsageAsync(int correctionId, CancellationToken ct = default);
}
