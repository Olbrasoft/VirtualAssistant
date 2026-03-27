namespace Olbrasoft.VirtualAssistant.Core.Models;

/// <summary>
/// Result of an LLM racing operation where multiple providers are called in parallel.
/// Contains the winner's result and a task for the loser's deferred result.
/// </summary>
/// <param name="WinnerResult">The correction result from the first provider to respond successfully.</param>
/// <param name="WinnerProviderName">Name of the winning provider (e.g., "mercury", "zen").</param>
/// <param name="RaceGroupId">Unique identifier linking both race participants for database tracking.</param>
/// <param name="LoserTask">Task that completes when the second provider responds. Null if only one provider was available.</param>
/// <param name="LoserProviderName">Name of the losing provider. Null if only one provider was available.</param>
public record RacingResult(
    LlmCorrectionResult WinnerResult,
    string WinnerProviderName,
    Guid RaceGroupId,
    Task<LlmCorrectionResult?>? LoserTask,
    string? LoserProviderName
);
