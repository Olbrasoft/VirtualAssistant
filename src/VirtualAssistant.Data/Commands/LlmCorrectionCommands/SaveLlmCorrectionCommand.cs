using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Data.Commands.LlmCorrectionCommands;

/// <summary>
/// Command to save a successful LLM correction to the database.
/// </summary>
/// <param name="VoiceTranscriptionId">ID of the voice transcription that was corrected. Must be greater than 0.</param>
/// <param name="CorrectedText">The text after LLM correction. Must not be null or empty.</param>
/// <param name="DurationMs">API call duration in milliseconds. Must be greater than 0.</param>
/// <param name="PromptId">ID of the prompt used for correction. NULL if no prompt tracking (legacy/skipped).</param>
/// <param name="ModelId">ID of the LLM model used. Required for all corrections.</param>
/// <param name="InputTokens">Number of input/prompt tokens consumed. NULL if not tracked.</param>
/// <param name="OutputTokens">Number of output/completion tokens generated. NULL if not tracked.</param>
/// <param name="ReasoningTokens">Number of reasoning tokens used. NULL if not applicable.</param>
public record SaveLlmCorrectionCommand(
    int VoiceTranscriptionId,
    string CorrectedText,
    int DurationMs,
    int? PromptId,
    int ModelId,
    int? InputTokens = null,
    int? OutputTokens = null,
    int? ReasoningTokens = null
) : ICommand<LlmCorrection>;
