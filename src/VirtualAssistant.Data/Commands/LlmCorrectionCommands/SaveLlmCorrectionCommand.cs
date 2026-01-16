using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Commands.LlmCorrectionCommands;

/// <summary>
/// Command to save a successful LLM correction to the database.
/// </summary>
/// <param name="WhisperTranscriptionId">ID of the Whisper transcription that was corrected. Must be greater than 0.</param>
/// <param name="CorrectedText">The text after LLM correction. Must not be null or empty.</param>
/// <param name="DurationMs">API call duration in milliseconds. Must be greater than 0.</param>
/// <param name="PromptId">ID of the prompt used for correction. NULL if no prompt tracking (legacy/skipped).</param>
/// <param name="ModelId">ID of the LLM model used. Required for all corrections.</param>
public record SaveLlmCorrectionCommand(
    int WhisperTranscriptionId,
    string CorrectedText,
    int DurationMs,
    int? PromptId,
    int ModelId
) : ICommand<LlmCorrection>;
