using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data;

/// <summary>
/// Repository for managing voice transcription records.
/// </summary>
public interface ITranscriptionRepository
{
    /// <summary>
    /// Saves a new transcription record.
    /// </summary>
    /// <param name="text">The transcribed text.</param>
    /// <param name="sourceApp">The application that was focused during dictation (optional).</param>
    /// <param name="durationMs">The recording duration in milliseconds (optional).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The saved transcription entity.</returns>
    Task<VoiceTranscription> SaveTranscriptionAsync(string text, string? sourceApp, int? durationMs, CancellationToken ct = default);

    /// <summary>
    /// Gets the most recent transcriptions.
    /// </summary>
    /// <param name="count">Maximum number of transcriptions to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of recent transcriptions ordered by creation date descending.</returns>
    Task<IReadOnlyList<VoiceTranscription>> GetRecentAsync(int count = 50, CancellationToken ct = default);

    /// <summary>
    /// Searches for transcriptions containing the specified query.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of matching transcriptions.</returns>
    Task<IReadOnlyList<VoiceTranscription>> SearchAsync(string query, CancellationToken ct = default);
}
