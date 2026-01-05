using Olbrasoft.VirtualAssistant.Core.Models;

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
    /// <param name="correctionResult">LLM correction result including corrected text, prompt ID, and duration (null if no correction applied)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ID of saved Whisper transcription, or null if save failed</returns>
    Task<int?> SaveTranscriptionAsync(
        byte[] audioData,
        string originalText,
        LlmCorrectionResult? correctionResult,
        CancellationToken cancellationToken = default);
}
