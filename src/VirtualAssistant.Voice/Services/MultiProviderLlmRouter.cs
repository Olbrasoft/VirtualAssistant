using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Enums;
using Olbrasoft.VirtualAssistant.Core.Exceptions;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Multi-provider LLM router with intelligent round-robin and rate limit handling.
/// Uses in-memory tracking of provider status (no database required).
/// </summary>
public class MultiProviderLlmRouter : ILlmRouterService
{
    private readonly ILogger<MultiProviderLlmRouter> _logger;
    private readonly Dictionary<LlmProvider, BaseLlmRouterService> _providerMap;
    
    // In-memory rate limit tracking (replaces database)
    private readonly ConcurrentDictionary<LlmProvider, DateTime> _rateLimitedUntil = new();
    
    // Round-robin state
    private int _lastProviderIndex = -1;
    private readonly LlmProvider[] _providerOrder;
    private readonly object _lockObject = new();

    public string ProviderName => "MultiProvider";

    public MultiProviderLlmRouter(
        ILogger<MultiProviderLlmRouter> logger,
        IEnumerable<BaseLlmRouterService> routers)
    {
        _logger = logger;
        
        // Build provider map from injected routers
        _providerMap = routers.ToDictionary(r => r.Provider, r => r);
        
        // Define provider order for round-robin (Mistral first as most reliable)
        _providerOrder = [LlmProvider.Mistral, LlmProvider.Groq, LlmProvider.Cerebras];

        _logger.LogInformation(
            "MultiProviderLlmRouter initialized with {Count} providers: {Providers}",
            _providerMap.Count,
            string.Join(", ", _providerMap.Keys));
    }

    public async Task<LlmRouterResult> RouteAsync(string inputText, bool isDiscussionActive = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inputText))
        {
            return LlmRouterResult.Ignored("Empty input");
        }

        var stopwatch = Stopwatch.StartNew();
        var errors = new List<string>();
        var triedProviders = new HashSet<LlmProvider>();

        // Try up to the number of providers we have
        for (int attempt = 0; attempt < _providerMap.Count; attempt++)
        {
            // Get next available provider from round-robin
            var nextProvider = GetNextAvailableProvider();
            
            if (nextProvider == null)
            {
                // All providers are rate limited
                _logger.LogWarning("All providers are currently rate limited");
                break;
            }

            var provider = nextProvider.Value;
            
            // Skip if we already tried this provider in this request
            if (!triedProviders.Add(provider))
            {
                continue;
            }

            if (!_providerMap.TryGetValue(provider, out var routerService))
            {
                _logger.LogWarning("No router service for provider {Provider}", provider);
                continue;
            }

            try
            {
                _logger.LogDebug("Trying provider: {Provider}", routerService.ProviderName);

                var result = await routerService.RouteAsync(inputText, isDiscussionActive, cancellationToken);

                if (result.Success)
                {
                    _logger.LogInformation(
                        "Provider {Provider} succeeded in {Time}ms (action: {Action})",
                        routerService.ProviderName,
                        result.ResponseTimeMs,
                        result.Action);

                    // Return result with provider name
                    return new LlmRouterResult
                    {
                        Success = result.Success,
                        Action = result.Action,
                        Confidence = result.Confidence,
                        Reason = result.Reason,
                        Response = result.Response,
                        CommandForOpenCode = result.CommandForOpenCode,
                        BashCommand = result.BashCommand,
                        NoteTitle = result.NoteTitle,
                        NoteContent = result.NoteContent,
                        DiscussionTopic = result.DiscussionTopic,
                        ResponseTimeMs = result.ResponseTimeMs,
                        ErrorMessage = result.ErrorMessage,
                        PromptType = result.PromptType,
                        ProviderName = routerService.ProviderName
                    };
                }

                var errorMsg = $"{routerService.ProviderName}: {result.ErrorMessage ?? "Unknown error"}";
                errors.Add(errorMsg);
                _logger.LogWarning(
                    "Provider {Provider} failed: {Error}. Trying next provider...",
                    routerService.ProviderName,
                    result.ErrorMessage);
            }
            catch (RateLimitException rle)
            {
                // Mark provider as rate limited in memory
                var resetAt = rle.ResetAt ?? DateTime.UtcNow.AddMinutes(5);
                MarkProviderRateLimited(provider, resetAt);

                var errorMsg = $"{routerService.ProviderName}: Rate limited until {resetAt:HH:mm:ss}";
                errors.Add(errorMsg);
                
                _logger.LogWarning(
                    "Provider {Provider} rate limited until {ResetAt}. Trying next provider...",
                    routerService.ProviderName,
                    resetAt);
            }
            catch (Exception ex)
            {
                var errorMsg = $"{routerService.ProviderName}: {ex.Message}";
                errors.Add(errorMsg);
                _logger.LogWarning(
                    ex,
                    "Provider {Provider} threw exception. Trying next provider...",
                    routerService.ProviderName);
            }
        }

        stopwatch.Stop();

        // All providers failed
        var allErrors = errors.Count > 0 
            ? string.Join("; ", errors) 
            : "All providers are rate limited";
            
        _logger.LogError(
            "All {Count} providers failed. Errors: {Errors}",
            _providerMap.Count,
            allErrors);

        return LlmRouterResult.Error(
            $"All providers failed: {allErrors}",
            (int)stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Gets the next available provider using round-robin.
    /// Skips providers that are currently rate limited.
    /// </summary>
    private LlmProvider? GetNextAvailableProvider()
    {
        lock (_lockObject)
        {
            var now = DateTime.UtcNow;
            
            // Clean up expired rate limits
            foreach (var provider in _rateLimitedUntil.Keys.ToList())
            {
                if (_rateLimitedUntil.TryGetValue(provider, out var until) && until <= now)
                {
                    _rateLimitedUntil.TryRemove(provider, out _);
                    _logger.LogInformation("Provider {Provider} rate limit expired", provider);
                }
            }
            
            // Try each provider in round-robin order
            for (int i = 0; i < _providerOrder.Length; i++)
            {
                _lastProviderIndex = (_lastProviderIndex + 1) % _providerOrder.Length;
                var provider = _providerOrder[_lastProviderIndex];
                
                // Check if provider is available (not rate limited and exists in map)
                if (_providerMap.ContainsKey(provider) && !_rateLimitedUntil.ContainsKey(provider))
                {
                    return provider;
                }
            }
            
            return null;
        }
    }

    /// <summary>
    /// Marks a provider as rate limited until the specified time.
    /// </summary>
    private void MarkProviderRateLimited(LlmProvider provider, DateTime resetAt)
    {
        _rateLimitedUntil[provider] = resetAt;
        _logger.LogInformation(
            "Provider {Provider} marked as rate limited until {ResetAt}",
            provider, resetAt);
    }
}
