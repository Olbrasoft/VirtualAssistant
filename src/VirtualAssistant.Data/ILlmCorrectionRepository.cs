using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data;

/// <summary>
/// Repository for managing LLM corrections.
/// </summary>
public interface ILlmCorrectionRepository
{
    /// <summary>
    /// Saves a successful LLM correction to the database.
    /// </summary>
    /// <param name="whisperTranscriptionId">ID of the Whisper transcription that was corrected.</param>
    /// <param name="correctedText">The text after LLM correction.</param>
    /// <param name="durationMs">API call duration in milliseconds.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The saved LLM correction entity.</returns>
    Task<LlmCorrection> SaveAsync(
        int whisperTranscriptionId,
        string correctedText,
        int durationMs,
        CancellationToken ct = default);

    /// <summary>
    /// Saves an LLM error to the database.
    /// </summary>
    /// <param name="whisperTranscriptionId">ID of the Whisper transcription that failed correction.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="durationMs">API call duration in milliseconds (how long before it failed).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The saved LLM error entity.</returns>
    Task<LlmError> SaveErrorAsync(
        int whisperTranscriptionId,
        string errorMessage,
        int durationMs,
        CancellationToken ct = default);
}
