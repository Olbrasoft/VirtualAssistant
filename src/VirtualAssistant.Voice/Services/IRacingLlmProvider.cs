using Olbrasoft.VirtualAssistant.Core.Models;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Calls multiple LLM providers in parallel and returns the first successful result.
/// The loser's result is available via a deferred task for background database persistence.
/// </summary>
public interface IRacingLlmProvider
{
    /// <summary>
    /// Sends text to multiple LLM providers simultaneously and returns the first successful correction.
    /// </summary>
    /// <param name="text">Text to correct.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Racing result with winner and deferred loser task, or null if all providers failed.</returns>
    Task<RacingResult?> CorrectTextAsync(string text, CancellationToken cancellationToken = default);
}
