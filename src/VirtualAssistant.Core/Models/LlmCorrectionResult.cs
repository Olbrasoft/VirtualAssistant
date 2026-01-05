namespace Olbrasoft.VirtualAssistant.Core.Models;

/// <summary>
/// Result of LLM text correction including metadata for database persistence.
/// </summary>
/// <param name="CorrectedText">The corrected text returned by the LLM.</param>
/// <param name="PromptId">ID of the prompt used for correction. NULL if no prompt tracking (legacy).</param>
/// <param name="DurationMs">Duration of the LLM API call in milliseconds.</param>
public record LlmCorrectionResult(
    string CorrectedText,
    int? PromptId,
    int DurationMs
);
