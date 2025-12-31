namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Service for persisting dictation transcriptions and LLM corrections to database.
/// Separates database concerns from dictation workflow orchestration.
/// </summary>
public interface IDictationPersistenceService
{
    /// <summary>
    /// Saves a Whisper transcription and optional LLM correction to database.
    /// </summary>
    /// <param name="audioData">Raw audio data (16-bit mono @ 16kHz)</param>
    /// <param name="originalText">Original Whisper transcription</param>
    /// <param name="correctedText">LLM-corrected text (null if no correction applied)</param>
    /// <param name="llmDurationMs">Duration of LLM correction call in milliseconds (0 if no correction)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ID of saved Whisper transcription, or null if save failed</returns>
    Task<int?> SaveTranscriptionAsync(
        byte[] audioData,
        string originalText,
        string? correctedText,
        int llmDurationMs,
        CancellationToken cancellationToken = default);
}
