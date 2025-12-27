namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Thread-safe cache for prompts with reload capability.
/// </summary>
public interface IPromptCache
{
    /// <summary>
    /// Gets a prompt by name, loading it from cache or source if not cached.
    /// </summary>
    /// <param name="promptName">The name of the prompt to retrieve.</param>
    /// <returns>The prompt content.</returns>
    string GetPrompt(string promptName);

    /// <summary>
    /// Clears all cached prompts, forcing them to be reloaded on next access.
    /// </summary>
    void ClearCache();

    /// <summary>
    /// Clears a specific prompt from cache.
    /// </summary>
    /// <param name="promptName">The name of the prompt to clear.</param>
    /// <returns>True if the prompt was in cache and removed, false otherwise.</returns>
    bool ClearPrompt(string promptName);
}
