using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Thread-safe cache for prompts with reload capability.
/// Allows clearing the cache to force reloading prompts from source.
/// </summary>
public class ReloadablePromptCache : IPromptCache
{
    private readonly IPromptLoader _loader;
    private readonly ILogger<ReloadablePromptCache> _logger;
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public ReloadablePromptCache(
        IPromptLoader loader,
        ILogger<ReloadablePromptCache> logger)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets a prompt by name, loading it from cache or source if not cached.
    /// </summary>
    /// <param name="promptName">The name of the prompt to retrieve.</param>
    /// <returns>The prompt content.</returns>
    public string GetPrompt(string promptName)
    {
        return _cache.GetOrAdd(promptName, name =>
        {
            _logger.LogDebug("Loading prompt '{PromptName}' into cache", name);
            return _loader.LoadPrompt(name);
        });
    }

    /// <summary>
    /// Clears all cached prompts, forcing them to be reloaded on next access.
    /// </summary>
    public void ClearCache()
    {
        var count = _cache.Count;
        _cache.Clear();
        _logger.LogInformation("Cleared {Count} prompts from cache", count);
    }

    /// <summary>
    /// Clears a specific prompt from cache.
    /// </summary>
    /// <param name="promptName">The name of the prompt to clear.</param>
    /// <returns>True if the prompt was in cache and removed, false otherwise.</returns>
    public bool ClearPrompt(string promptName)
    {
        var removed = _cache.TryRemove(promptName, out _);
        if (removed)
        {
            _logger.LogInformation("Cleared prompt '{PromptName}' from cache", promptName);
        }
        return removed;
    }
}
