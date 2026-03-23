namespace Olbrasoft.VirtualAssistant.Core.Models;

/// <summary>
/// Result of LLM text correction including metadata for database persistence.
/// </summary>
/// <param name="CorrectedText">The corrected text returned by the LLM.</param>
/// <param name="PromptId">ID of the prompt used for correction. NULL if no prompt tracking (legacy).</param>
/// <param name="DurationMs">Duration of the LLM API call in milliseconds.</param>
/// <param name="ModelId">ID of the LLM model used. NULL for legacy corrections or when skipped.</param>
/// <param name="InputTokens">Number of input/prompt tokens consumed. NULL if not tracked.</param>
/// <param name="OutputTokens">Number of output/completion tokens generated. NULL if not tracked.</param>
/// <param name="ReasoningTokens">Number of reasoning tokens used (Mercury 2). NULL if not applicable.</param>
public record LlmCorrectionResult(
    string CorrectedText,
    int? PromptId,
    int DurationMs,
    int? ModelId = null,
    int? InputTokens = null,
    int? OutputTokens = null,
    int? ReasoningTokens = null
);
