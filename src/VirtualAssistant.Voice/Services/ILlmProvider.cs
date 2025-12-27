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
    /// <returns>Corrected text</returns>
    Task<string> CorrectTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the API usage information from the last response headers.
    /// </summary>
    /// <returns>Dictionary of rate limit headers</returns>
    Dictionary<string, string> GetLastRateLimitHeaders();
}
