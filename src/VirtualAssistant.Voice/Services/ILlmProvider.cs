using Olbrasoft.VirtualAssistant.Core.Models;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Represents an LLM provider for text correction.
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Gets the provider name (e.g., 'mistral', 'groq').
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the model name (e.g., 'mistral-large-latest').
    /// </summary>
    string ModelName { get; }

    /// <summary>
    /// Corrects the given text using the LLM.
    /// </summary>
    /// <param name="text">Text to correct</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>LLM correction result including corrected text, prompt ID, and duration</returns>
    Task<LlmCorrectionResult> CorrectTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the API usage information from the last response headers.
    /// </summary>
    /// <returns>Dictionary of rate limit headers</returns>
    Dictionary<string, string> GetLastRateLimitHeaders();

    /// <summary>
    /// Enables or disables the provider at runtime.
    /// </summary>
    /// <param name="enabled">True to enable, false to disable</param>
    void SetEnabled(bool enabled);

    /// <summary>
    /// Gets the current runtime enabled state.
    /// </summary>
    /// <returns>True if provider is enabled, false otherwise</returns>
    bool IsEnabled();

    /// <summary>
    /// Reloads the system prompt from disk, clearing any cached version.
    /// </summary>
    void ReloadPrompt();
}
