using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Data.Queries.ProviderQueries;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Factory for creating and managing speech transcriber instances.
/// Uses explicit provider injection instead of keyed services to avoid DI container bugs.
/// Caches provider IDs from database for efficient tracking.
/// </summary>
public class SpeechTranscriberFactory : ISpeechTranscriberFactory
{
    private readonly IReadOnlyDictionary<string, ISpeechTranscriber> _providersByKey;
    private readonly IQueryProcessor _queryProcessor;
    private readonly SpeechProviderSettings _settings;
    private readonly ILogger<SpeechTranscriberFactory> _logger;

    // Cache for provider IDs - loaded from database ONCE at first use
    private Dictionary<string, int>? _providerIdCache;
    private readonly object _cacheLock = new();

    /// <summary>
    /// Initializes a new instance of <see cref="SpeechTranscriberFactory"/>.
    /// Providers self-declare their <see cref="ISpeechTranscriber.ProviderKey"/> and
    /// <see cref="ISpeechTranscriber.DatabaseName"/> — the factory just indexes them,
    /// so adding a new STT provider requires no edits here.
    /// </summary>
    public SpeechTranscriberFactory(
        IEnumerable<ISpeechTranscriber> providers,
        IQueryProcessor queryProcessor,
        IOptions<SpeechProviderSettings> settings,
        ILogger<SpeechTranscriberFactory> logger)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _queryProcessor = queryProcessor ?? throw new ArgumentNullException(nameof(queryProcessor));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _providersByKey = BuildProviderDictionary(providers);

        _logger.LogInformation(
            "SpeechTranscriberFactory initialized with providers: {Providers}",
            string.Join(", ", _providersByKey.Select(p => $"{p.Key}={p.Value.GetType().Name}")));
    }

    private static IReadOnlyDictionary<string, ISpeechTranscriber> BuildProviderDictionary(
        IEnumerable<ISpeechTranscriber> providers)
    {
        var result = new Dictionary<string, ISpeechTranscriber>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in providers)
        {
            if (string.IsNullOrWhiteSpace(provider.ProviderKey))
            {
                throw new InvalidOperationException(
                    $"Provider '{provider.GetType().FullName}' returned a null/empty ProviderKey. " +
                    "Each ISpeechTranscriber implementation must expose a non-empty identifier.");
            }

            if (result.ContainsKey(provider.ProviderKey))
            {
                throw new InvalidOperationException(
                    $"Duplicate ISpeechTranscriber.ProviderKey '{provider.ProviderKey}' " +
                    $"(clash between {result[provider.ProviderKey].GetType().FullName} and {provider.GetType().FullName}).");
            }

            result.Add(provider.ProviderKey, provider);
        }

        if (result.Count == 0)
        {
            throw new InvalidOperationException(
                "SpeechTranscriberFactory requires at least one ISpeechTranscriber registration.");
        }

        return result;
    }

    /// <summary>
    /// Gets the currently active speech transcriber based on configuration.
    /// </summary>
    public ISpeechTranscriber GetActiveProvider()
    {
        var providerName = _settings.PrimaryProvider;
        return GetProvider(providerName)
            ?? throw new InvalidOperationException($"Primary STT provider '{providerName}' is not available");
    }

    /// <summary>
    /// Gets a specific provider by name. Returns null when no matching provider is registered.
    /// Lookup is case-insensitive on <see cref="ISpeechTranscriber.ProviderKey"/>.
    /// </summary>
    public ISpeechTranscriber? GetProvider(string providerName)
    {
        if (string.IsNullOrEmpty(providerName))
            return null;

        if (_providersByKey.TryGetValue(providerName, out var provider))
        {
            _logger.LogDebug(
                "GetProvider({ProviderName}) returning {ProviderType}",
                providerName,
                provider.GetType().Name);
            return provider;
        }

        return null;
    }

    /// <summary>
    /// Gets all available provider keys.
    /// </summary>
    public IReadOnlyCollection<string> GetAvailableProviders() => _providersByKey.Keys.ToArray();

    /// <summary>
    /// Gets the database provider ID for tracking.
    /// Returns from cache - loaded once at startup, no DB query per call.
    /// </summary>
    public int GetProviderId(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name must be a non-empty string.", nameof(providerName));
        }

        EnsureProviderIdCacheLoaded();

        // Provider implementations expose their own DatabaseName so the mapping
        // has a single source of truth and new providers don't require factory edits.
        if (!_providersByKey.TryGetValue(providerName, out var provider))
        {
            throw new ArgumentException(
                $"Unknown STT provider: '{providerName}'. Available: {string.Join(", ", _providersByKey.Keys)}",
                nameof(providerName));
        }

        if (_providerIdCache!.TryGetValue(provider.DatabaseName, out var id))
        {
            return id;
        }

        throw new ArgumentException(
            $"Provider '{providerName}' has DatabaseName '{provider.DatabaseName}' which is not in the providers table. " +
            $"Known rows: {string.Join(", ", _providerIdCache.Keys)}",
            nameof(providerName));
    }

    /// <summary>
    /// Pre-loads provider IDs from database into the in-memory cache.
    /// <c>SpeechTranscriberFactoryWarmupService</c> calls this during application
    /// startup, so the hot path (<see cref="GetProviderId"/>) never has to trigger
    /// the synchronous fallback below.
    /// </summary>
    public async Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        if (_providerIdCache != null) return;

        var providers = await _queryProcessor
            .ProcessAsync(new GetProvidersByTypeQuery("stt"), cancellationToken)
            .ConfigureAwait(false);

        lock (_cacheLock)
        {
            if (_providerIdCache != null) return;

            _providerIdCache = providers.ToDictionary(
                p => p.Name,
                p => p.Id,
                StringComparer.OrdinalIgnoreCase);
        }

        _logger.LogInformation(
            "Warmed {Count} STT providers from database: {Providers}",
            _providerIdCache.Count,
            string.Join(", ", _providerIdCache.Select(p => $"{p.Key}={p.Value}")));
    }

    /// <summary>
    /// Loads provider IDs from database into cache via synchronous blocking.
    /// In production this is unreachable because the warmup hosted service
    /// populates the cache at startup before any transcription pipeline runs;
    /// it remains as a defensive fallback for test environments where
    /// <see cref="WarmupAsync"/> was not invoked. The sync-over-async is
    /// acceptable on that cold-start path because the caller is inside a
    /// double-checked lock and the blocking call happens at most once per
    /// process lifetime.
    /// </summary>
    private void EnsureProviderIdCacheLoaded()
    {
        if (_providerIdCache != null) return;

        lock (_cacheLock)
        {
            if (_providerIdCache != null) return;

            var providers = _queryProcessor.ProcessAsync(
                new GetProvidersByTypeQuery("stt"), CancellationToken.None).GetAwaiter().GetResult();

            _providerIdCache = providers.ToDictionary(
                p => p.Name,
                p => p.Id,
                StringComparer.OrdinalIgnoreCase);

            _logger.LogInformation(
                "Loaded {Count} STT providers from database (cold-start fallback): {Providers}",
                _providerIdCache.Count,
                string.Join(", ", _providerIdCache.Select(p => $"{p.Key}={p.Value}")));
        }
    }
}
