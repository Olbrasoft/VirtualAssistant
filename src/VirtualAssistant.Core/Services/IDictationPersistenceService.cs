using Olbrasoft.VirtualAssistant.Core.Models;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Service for persisting dictation transcriptions and LLM corrections to database.
/// Separates database concerns from dictation workflow orchestration.
/// </summary>
public interface IDictationPersistenceService
{
    /// <summary>
    /// Saves a voice transcription and optional LLM correction to database.
    /// </summary>
    /// <param name="audioData">Raw audio data (16-bit mono @ 16kHz)</param>
    /// <param name="originalText">Original STT transcription</param>
    /// <param name="correctionResult">LLM correction result including corrected text, prompt ID, and duration (null if no correction applied)</param>
    /// <param name="sttProviderId">ID of the STT provider that performed the transcription (e.g., Google=14, Whisper=13)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ID of saved voice transcription, or null if save failed</returns>
    Task<int?> SaveTranscriptionAsync(
        byte[] audioData,
        string originalText,
        LlmCorrectionResult? correctionResult,
        int sttProviderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a voice transcription with racing LLM correction.
    /// Winner correction is saved immediately; loser correction is saved asynchronously when it arrives.
    /// </summary>
    /// <param name="audioData">Raw audio data (16-bit mono @ 16kHz)</param>
    /// <param name="originalText">Original STT transcription</param>
    /// <param name="correctionResult">Winner's LLM correction result</param>
    /// <param name="sttProviderId">ID of the STT provider</param>
    /// <param name="raceGroupId">Unique identifier for this race</param>
    /// <param name="loserTask">Deferred task for the loser's result (null if no race)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ID of saved voice transcription, or null if save failed</returns>
    Task<int?> SaveTranscriptionWithRacingAsync(
        byte[] audioData,
        string originalText,
        LlmCorrectionResult? correctionResult,
        int sttProviderId,
        Guid raceGroupId,
        Task<LlmCorrectionResult?>? loserTask,
        CancellationToken cancellationToken = default);
}
