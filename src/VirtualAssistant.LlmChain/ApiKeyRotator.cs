using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.LlmChain.Configuration;

namespace Olbrasoft.VirtualAssistant.LlmChain;

/// <summary>
/// Manages API key rotation and rate limit tracking for LLM providers.
/// Cycles through keys in round-robin fashion with rate limit awareness.
/// </summary>
public class ApiKeyRotator : IApiKeyRotator
{
    private readonly ILogger<ApiKeyRotator> _logger;

    // Cache effective keys per provider (loaded from files at startup)
    private readonly Dictionary<string, List<string>> _effectiveKeys = new();

    // Track rate-limited provider+key combinations
    private readonly ConcurrentDictionary<string, DateTime> _rateLimitedUntil = new();

    // Round-robin state for key rotation (per provider)
    private readonly ConcurrentDictionary<string, int> _lastKeyIndex = new();

    public ApiKeyRotator(
        ILogger<ApiKeyRotator> logger,
        IOptions<LlmChainOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(options);

        var llmOptions = options.Value;

        // Load effective keys for each provider (from files or inline config)
        foreach (var provider in llmOptions.Providers.Where(p => p.Enabled))
        {
            _effectiveKeys[provider.Name] = provider.GetEffectiveApiKeys();
        }

        _logger.LogDebug(
            "ApiKeyRotator initialized with keys for {Count} providers",
            _effectiveKeys.Count);
    }

    /// <inheritdoc />
    public int GetKeyCount(string providerName) =>
        _effectiveKeys.TryGetValue(providerName, out var keys) ? keys.Count : 0;

    /// <inheritdoc />
    public (string? Key, int Index) GetNextAvailableKey(string providerName)
    {
        if (!_effectiveKeys.TryGetValue(providerName, out var keys) || keys.Count == 0)
            return (null, -1);

        var lastIndex = _lastKeyIndex.GetOrAdd(providerName, -1);

        for (int i = 0; i < keys.Count; i++)
        {
            var keyIndex = (lastIndex + 1 + i) % keys.Count;
            var providerKeyId = $"{providerName}:{keyIndex}";

            if (!_rateLimitedUntil.ContainsKey(providerKeyId))
            {
                _lastKeyIndex[providerName] = keyIndex;
                return (keys[keyIndex], keyIndex);
            }
        }

        return (null, -1);
    }

    /// <inheritdoc />
    public void MarkRateLimited(string providerName, int keyIndex, DateTime resetAt)
    {
        var providerKeyId = $"{providerName}:{keyIndex}";
        _rateLimitedUntil[providerKeyId] = resetAt;
        _logger.LogDebug("Marked {ProviderKeyId} as rate limited until {ResetAt}", providerKeyId, resetAt);
    }

    /// <inheritdoc />
    public bool HasAvailableKey(string providerName)
    {
        var keyCount = GetKeyCount(providerName);
        for (int k = 0; k < keyCount; k++)
        {
            var providerKeyId = $"{providerName}:{k}";
            if (!_rateLimitedUntil.ContainsKey(providerKeyId))
            {
                return true;
            }
        }
        return false;
    }

    /// <inheritdoc />
    public void CleanupExpiredRateLimits()
    {
        var now = DateTime.UtcNow;
        foreach (var key in _rateLimitedUntil.Keys.ToList())
        {
            if (_rateLimitedUntil.TryGetValue(key, out var until) && until <= now)
            {
                _rateLimitedUntil.TryRemove(key, out _);
                _logger.LogDebug("Rate limit expired for {Key}", key);
            }
        }
    }

    /// <inheritdoc />
    public string MaskKey(string key)
    {
        if (key.Length <= 8) return "****";
        return $"{key[..4]}...{key[^4..]}";
    }
}
