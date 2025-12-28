namespace VirtualAssistant.Data;

/// <summary>
/// Repository for managing ASR transcription corrections.
/// </summary>
public interface ITranscriptionCorrectionRepository
{
    /// <summary>
    /// Gets all active corrections ordered by priority (highest first).
    /// Used by TextFilter for in-memory caching.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Read-only list of active corrections.</returns>
    Task<IReadOnlyList<Entities.TranscriptionCorrection>> GetActiveCorrectionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets a correction by its unique identifier.
    /// </summary>
    /// <param name="id">The correction ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The correction if found, otherwise null.</returns>
    Task<Entities.TranscriptionCorrection?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Adds a new correction to the database.
    /// </summary>
    /// <param name="correction">The correction to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task completing when the correction is added.</returns>
    Task AddAsync(Entities.TranscriptionCorrection correction, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing correction in the database.
    /// Sets UpdatedAt to current time automatically.
    /// </summary>
    /// <param name="correction">The correction to update.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task completing when the correction is updated.</returns>
    Task UpdateAsync(Entities.TranscriptionCorrection correction, CancellationToken ct = default);

    /// <summary>
    /// Deletes a correction from the database.
    /// </summary>
    /// <param name="id">The ID of the correction to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task completing when the correction is deleted.</returns>
    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Tracks that a correction was applied during transcription processing.
    /// Used for analytics to measure correction effectiveness.
    /// </summary>
    /// <param name="correctionId">The ID of the applied correction.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task completing when usage is tracked.</returns>
    Task TrackUsageAsync(int correctionId, CancellationToken ct = default);
}
