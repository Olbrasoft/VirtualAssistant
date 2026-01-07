namespace Olbrasoft.VirtualAssistant.LlmChain;

/// <summary>
/// Manages API key rotation and rate limit tracking for LLM providers.
/// Provides round-robin key selection with rate limit awareness.
/// </summary>
public interface IApiKeyRotator
{
    /// <summary>
    /// Gets the next available API key for the specified provider.
    /// Returns null if all keys are rate limited.
    /// </summary>
    /// <param name="providerName">Name of the LLM provider</param>
    /// <returns>API key and its index, or null if none available</returns>
    (string? Key, int Index) GetNextAvailableKey(string providerName);

    /// <summary>
    /// Gets the count of keys configured for the specified provider.
    /// </summary>
    int GetKeyCount(string providerName);

    /// <summary>
    /// Marks a specific provider+key combination as rate limited.
    /// </summary>
    /// <param name="providerName">Name of the provider</param>
    /// <param name="keyIndex">Index of the key</param>
    /// <param name="resetAt">When the rate limit expires</param>
    void MarkRateLimited(string providerName, int keyIndex, DateTime resetAt);

    /// <summary>
    /// Checks if any key is available (not rate limited) for the provider.
    /// </summary>
    bool HasAvailableKey(string providerName);

    /// <summary>
    /// Cleans up expired rate limits.
    /// </summary>
    void CleanupExpiredRateLimits();

    /// <summary>
    /// Masks an API key for logging (shows first and last 4 chars).
    /// </summary>
    string MaskKey(string key);
}
