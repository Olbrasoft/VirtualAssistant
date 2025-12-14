using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtualAssistant.LlmChain.Configuration;

namespace VirtualAssistant.LlmChain;

/// <summary>
/// LLM chain client with intelligent provider failover and key rotation.
/// Cycles through providers and keys in round-robin fashion with rate limit handling.
/// </summary>
public class LlmChainClient : ILlmChainClient
{
    private readonly ILogger<LlmChainClient> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LlmChainOptions _options;

    // Cache effective keys per provider (loaded from files at startup)
    private readonly Dictionary<string, List<string>> _effectiveKeys = new();

    // Track rate-limited provider+key combinations
    private readonly ConcurrentDictionary<string, DateTime> _rateLimitedUntil = new();

    // Round-robin state for provider rotation
    private int _lastProviderIndex = -1;
    private readonly object _providerLock = new();

    // Round-robin state for key rotation (per provider)
    private readonly ConcurrentDictionary<string, int> _lastKeyIndex = new();

    public LlmChainClient(
        ILogger<LlmChainClient> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<LlmChainOptions> options)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;

        // Load effective keys for each provider (from files or inline config)
        foreach (var provider in _options.Providers.Where(p => p.Enabled))
        {
            _effectiveKeys[provider.Name] = provider.GetEffectiveApiKeys();
        }

        var enabledProviders = _options.Providers.Where(p => p.Enabled).ToList();
        _logger.LogInformation(
            "LlmChainClient initialized with {Count} providers: {Providers}",
            enabledProviders.Count,
            string.Join(", ", enabledProviders.Select(p => $"{p.Name}({GetKeyCount(p.Name)} keys)")));
    }

    private int GetKeyCount(string providerName) =>
        _effectiveKeys.TryGetValue(providerName, out var keys) ? keys.Count : 0;

    public async Task<LlmChainResult> CompleteAsync(LlmChainRequest request, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var attempts = new List<ProviderAttempt>();
        var enabledProviders = _options.Providers.Where(p => p.Enabled && GetKeyCount(p.Name) > 0).ToList();

        if (enabledProviders.Count == 0)
        {
            return LlmChainResult.Fail("No enabled providers with API keys configured", attempts);
        }

        // Clean up expired rate limits
        CleanupExpiredRateLimits();

        // Try each provider with each key (worst case: all providers * all keys)
        var maxAttempts = enabledProviders.Sum(p => GetKeyCount(p.Name));
        var attemptCount = 0;

        while (attemptCount < maxAttempts)
        {
            // Get next provider in round-robin
            var provider = GetNextAvailableProvider(enabledProviders);
            if (provider == null)
            {
                _logger.LogWarning("All provider/key combinations are rate limited");
                break;
            }

            // Get next key for this provider in round-robin
            var (apiKey, keyIndex) = GetNextAvailableKey(provider);
            if (apiKey == null)
            {
                attemptCount++;
                continue;
            }

            var keyId = MaskKey(apiKey);
            var providerKeyId = $"{provider.Name}:{keyIndex}";

            // Skip if this provider+key is rate limited
            if (_rateLimitedUntil.ContainsKey(providerKeyId))
            {
                attemptCount++;
                continue;
            }

            try
            {
                _logger.LogDebug("Trying {Provider} with key {KeyId}", provider.Name, keyId);

                var result = await CallProviderAsync(provider, apiKey, request, ct);

                if (result.Success)
                {
                    stopwatch.Stop();
                    _logger.LogInformation(
                        "{Provider} succeeded in {Time}ms (key {KeyId})",
                        provider.Name, stopwatch.ElapsedMilliseconds, keyId);

                    return LlmChainResult.Ok(
                        result.Content!,
                        provider.Name,
                        keyId,
                        (int)stopwatch.ElapsedMilliseconds,
                        attempts);
                }

                // Non-success result
                attempts.Add(new ProviderAttempt
                {
                    Provider = provider.Name,
                    KeyId = keyId,
                    Error = result.Error ?? "Unknown error",
                    WasRateLimited = false
                });

                _logger.LogWarning(
                    "{Provider} failed: {Error}. Trying next...",
                    provider.Name, result.Error);
            }
            catch (RateLimitedException rle)
            {
                // Mark this provider+key as rate limited
                var resetAt = rle.ResetAt ?? DateTime.UtcNow.Add(_options.RateLimitCooldown);
                _rateLimitedUntil[providerKeyId] = resetAt;

                attempts.Add(new ProviderAttempt
                {
                    Provider = provider.Name,
                    KeyId = keyId,
                    Error = $"Rate limited until {resetAt:HH:mm:ss}",
                    WasRateLimited = true
                });

                _logger.LogWarning(
                    "{Provider} rate limited until {ResetAt}. Trying next...",
                    provider.Name, resetAt);
            }
            catch (Exception ex)
            {
                attempts.Add(new ProviderAttempt
                {
                    Provider = provider.Name,
                    KeyId = keyId,
                    Error = ex.Message,
                    WasRateLimited = false
                });

                _logger.LogWarning(ex, "{Provider} threw exception. Trying next...", provider.Name);
            }

            attemptCount++;
        }

        stopwatch.Stop();
        var allErrors = string.Join("; ", attempts.Select(a => $"{a.Provider}: {a.Error}"));
        return LlmChainResult.Fail($"All providers failed: {allErrors}", attempts);
    }

    private async Task<(bool Success, string? Content, string? Error)> CallProviderAsync(
        LlmProviderConfig provider,
        string apiKey,
        LlmChainRequest request,
        CancellationToken ct)
    {
        var httpClient = _httpClientFactory.CreateClient($"LlmChain_{provider.Name}");
        httpClient.BaseAddress = new Uri(provider.BaseUrl);
        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        httpClient.Timeout = _options.RequestTimeout;

        var llmRequest = new LlmApiRequest
        {
            Model = provider.Model,
            Messages =
            [
                new LlmMessage { Role = "system", Content = request.SystemPrompt },
                new LlmMessage { Role = "user", Content = request.UserMessage }
            ],
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens
        };

        var requestJson = JsonSerializer.Serialize(llmRequest);
        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync("chat/completions", content, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            var resetAt = ParseResetTimeFromError(errorBody);
            throw new RateLimitedException(provider.Name, resetAt);
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            return (false, null, $"HTTP {(int)response.StatusCode}: {errorBody}");
        }

        var llmResponse = await response.Content.ReadFromJsonAsync<LlmApiResponse>(cancellationToken: ct);
        var responseContent = llmResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();

        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return (false, null, "Empty response from API");
        }

        return (true, responseContent, null);
    }

    private LlmProviderConfig? GetNextAvailableProvider(List<LlmProviderConfig> providers)
    {
        lock (_providerLock)
        {
            for (int i = 0; i < providers.Count; i++)
            {
                _lastProviderIndex = (_lastProviderIndex + 1) % providers.Count;
                var provider = providers[_lastProviderIndex];
                var keyCount = GetKeyCount(provider.Name);

                // Check if at least one key is not rate limited
                for (int k = 0; k < keyCount; k++)
                {
                    var providerKeyId = $"{provider.Name}:{k}";
                    if (!_rateLimitedUntil.ContainsKey(providerKeyId))
                    {
                        return provider;
                    }
                }
            }

            return null;
        }
    }

    private (string? Key, int Index) GetNextAvailableKey(LlmProviderConfig provider)
    {
        if (!_effectiveKeys.TryGetValue(provider.Name, out var keys) || keys.Count == 0)
            return (null, -1);

        var lastIndex = _lastKeyIndex.GetOrAdd(provider.Name, -1);

        for (int i = 0; i < keys.Count; i++)
        {
            var keyIndex = (lastIndex + 1 + i) % keys.Count;
            var providerKeyId = $"{provider.Name}:{keyIndex}";

            if (!_rateLimitedUntil.ContainsKey(providerKeyId))
            {
                _lastKeyIndex[provider.Name] = keyIndex;
                return (keys[keyIndex], keyIndex);
            }
        }

        return (null, -1);
    }

    private void CleanupExpiredRateLimits()
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

    private static DateTime? ParseResetTimeFromError(string errorBody)
    {
        try
        {
            // Pattern: "Please try again in Xm Y.Zs" or "Please try again in Y.Zs"
            var match = Regex.Match(errorBody, @"try again in (\d+)m([\d.]+)s");
            if (match.Success)
            {
                var minutes = int.Parse(match.Groups[1].Value);
                var seconds = double.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                return DateTime.UtcNow.AddMinutes(minutes).AddSeconds(seconds);
            }

            match = Regex.Match(errorBody, @"try again in ([\d.]+)s");
            if (match.Success)
            {
                var seconds = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                return DateTime.UtcNow.AddSeconds(seconds);
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return null;
    }

    private static string MaskKey(string key)
    {
        if (key.Length <= 8) return "****";
        return $"{key[..4]}...{key[^4..]}";
    }

    #region DTOs

    private class LlmApiRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; set; }

        [JsonPropertyName("messages")]
        public required LlmMessage[] Messages { get; set; }

        [JsonPropertyName("temperature")]
        public float Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }
    }

    private class LlmMessage
    {
        [JsonPropertyName("role")]
        public required string Role { get; set; }

        [JsonPropertyName("content")]
        public required string Content { get; set; }
    }

    private class LlmApiResponse
    {
        [JsonPropertyName("choices")]
        public LlmChoice[]? Choices { get; set; }
    }

    private class LlmChoice
    {
        [JsonPropertyName("message")]
        public LlmMessage? Message { get; set; }
    }

    #endregion
}

/// <summary>
/// Exception thrown when a provider returns 429 (rate limited).
/// </summary>
public class RateLimitedException : Exception
{
    public string ProviderName { get; }
    public DateTime? ResetAt { get; }

    public RateLimitedException(string providerName, DateTime? resetAt = null)
        : base($"Provider {providerName} rate limited")
    {
        ProviderName = providerName;
        ResetAt = resetAt;
    }
}
