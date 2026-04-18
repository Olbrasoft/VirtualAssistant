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

        _providersByKey = providers.ToDictionary(
            p => p.ProviderKey,
            p => p,
            StringComparer.OrdinalIgnoreCase);

        if (_providersByKey.Count == 0)
        {
            throw new InvalidOperationException(
                "SpeechTranscriberFactory requires at least one ISpeechTranscriber registration.");
        }

        _logger.LogInformation(
            "SpeechTranscriberFactory initialized with providers: {Providers}",
            string.Join(", ", _providersByKey.Select(p => $"{p.Key}={p.Value.GetType().Name}")));
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
    /// Loads provider IDs from database into cache. Called ONCE (lazy loading).
    /// Thread-safe with double-check locking pattern.
    /// </summary>
    /// <remarks>
    /// This method uses synchronous blocking (.GetAwaiter().GetResult()) intentionally.
    /// The cache loading happens only ONCE during first use (startup-time lazy loading),
    /// not during request processing. This pattern is acceptable here because:
    /// 1. Single DB query executed at most once per application lifetime
    /// 2. No synchronization context issues in this singleton factory
    /// 3. Avoids adding async complexity to GetProviderId API used by transcription pipeline
    /// </remarks>
    private void EnsureProviderIdCacheLoaded()
    {
        if (_providerIdCache != null) return;

        lock (_cacheLock)
        {
            if (_providerIdCache != null) return;

            // Load all STT providers from database - THIS IS THE ONLY DB QUERY
            // Synchronous call is acceptable here - see remarks above
            var providers = _queryProcessor.ProcessAsync(
                new GetProvidersByTypeQuery("stt"), CancellationToken.None).GetAwaiter().GetResult();

            _providerIdCache = providers.ToDictionary(
                p => p.Name,
                p => p.Id,
                StringComparer.OrdinalIgnoreCase);

            _logger.LogInformation(
                "Loaded {Count} STT providers from database: {Providers}",
                _providerIdCache.Count,
                string.Join(", ", _providerIdCache.Select(p => $"{p.Key}={p.Value}")));
        }
    }
}
