using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Utilities;
using Olbrasoft.VirtualAssistant.LlmChain.Configuration;
using Olbrasoft.VirtualAssistant.LlmChain.Dtos;
using Olbrasoft.VirtualAssistant.LlmChain.Exceptions;

namespace Olbrasoft.VirtualAssistant.LlmChain;

/// <summary>
/// LLM chain client with intelligent provider failover and key rotation.
/// Orchestrates LLM chain execution using injected services for key rotation
/// and request building (SRP compliant).
/// </summary>
public class LlmChainClient : ILlmChainClient
{
    private readonly ILogger<LlmChainClient> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IApiKeyRotator _keyRotator;
    private readonly ILlmRequestBuilder _requestBuilder;
    private readonly LlmChainOptions _options;

    // Round-robin state for provider rotation
    private int _lastProviderIndex = -1;
    private readonly object _providerLock = new();

    public LlmChainClient(
        ILogger<LlmChainClient> logger,
        IHttpClientFactory httpClientFactory,
        IApiKeyRotator keyRotator,
        ILlmRequestBuilder requestBuilder,
        IOptions<LlmChainOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _keyRotator = keyRotator ?? throw new ArgumentNullException(nameof(keyRotator));
        _requestBuilder = requestBuilder ?? throw new ArgumentNullException(nameof(requestBuilder));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;

        var enabledProviders = _options.Providers.Where(p => p.Enabled).ToList();
        _logger.LogInformation(
            "LlmChainClient initialized with {Count} providers: {Providers}",
            enabledProviders.Count,
            string.Join(", ", enabledProviders.Select(p => $"{p.Name}({_keyRotator.GetKeyCount(p.Name)} keys)")));
    }

    public async Task<LlmChainResult> CompleteAsync(LlmChainRequest request, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var attempts = new List<ProviderAttempt>();
        var enabledProviders = _options.Providers
            .Where(p => p.Enabled && _keyRotator.GetKeyCount(p.Name) > 0)
            .ToList();

        if (enabledProviders.Count == 0)
        {
            return LlmChainResult.Fail("No enabled providers with API keys configured", attempts);
        }

        // Clean up expired rate limits
        _keyRotator.CleanupExpiredRateLimits();

        // Try each provider with each key (worst case: all providers * all keys)
        var maxAttempts = enabledProviders.Sum(p => _keyRotator.GetKeyCount(p.Name));
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
            var (apiKey, keyIndex) = _keyRotator.GetNextAvailableKey(provider.Name);
            if (apiKey == null)
            {
                attemptCount++;
                continue;
            }

            var keyId = _keyRotator.MaskKey(apiKey);

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
                _keyRotator.MarkRateLimited(provider.Name, keyIndex, resetAt);

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
        _requestBuilder.ConfigureHttpClient(httpClient, provider, apiKey);

        using var content = _requestBuilder.BuildRequestContent(request, provider);

        var response = await httpClient.PostAsync("chat/completions", content, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            var resetAt = RateLimitParser.ParseResetTimeFromError(errorBody);
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

                // Check if at least one key is not rate limited
                if (_keyRotator.HasAvailableKey(provider.Name))
                {
                    return provider;
                }
            }

            return null;
        }
    }
}
