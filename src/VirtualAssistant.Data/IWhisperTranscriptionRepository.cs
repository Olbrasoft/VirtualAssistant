using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data;

/// <summary>
/// Repository for managing Whisper AI transcriptions.
/// </summary>
public interface IWhisperTranscriptionRepository
{
    /// <summary>
    /// Saves a new Whisper transcription to the database.
    /// </summary>
    /// <param name="text">The transcribed text.</param>
    /// <param name="durationMs">Optional audio duration in milliseconds.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The saved transcription entity with generated ID.</returns>
    Task<WhisperTranscription> SaveAsync(string text, int? durationMs = null, CancellationToken ct = default);

    /// <summary>
    /// Gets the most recent transcriptions.
    /// </summary>
    /// <param name="count">Number of recent transcriptions to retrieve (default: 50).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of recent transcriptions ordered by creation time (newest first).</returns>
    Task<IReadOnlyList<WhisperTranscription>> GetRecentAsync(int count = 50, CancellationToken ct = default);

    /// <summary>
    /// Searches transcriptions by text content.
    /// </summary>
    /// <param name="query">Search query (case-insensitive partial match).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of matching transcriptions.</returns>
    Task<IReadOnlyList<WhisperTranscription>> SearchAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// Gets the latest corrected text (LLM correction if available, otherwise Whisper text).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Latest corrected text or null if no transcriptions exist.</returns>
    Task<string?> GetLatestCorrectedTextAsync(CancellationToken ct = default);
}
