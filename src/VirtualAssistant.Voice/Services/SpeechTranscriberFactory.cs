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

    // Cache for provider IDs - loaded from database ONCE at first use.
    // All reads and writes go through Volatile helpers so publication of the
    // populated dictionary is reliably visible to other threads without
    // requiring them to enter the lock. (Copilot review on PR #1011.)
    private Dictionary<string, int>? _providerIdCache;
    private readonly object _cacheLock = new();

    // Shared in-flight warmup task. Concurrent WarmupAsync callers latch
    // onto the same task so only one DB round-trip fires while a warmup is
    // in flight; EnsureProviderIdCacheLoaded also observes this task so the
    // sync cold-start path doesn't issue a second query while warmup is
    // still running. Reset to null when the task completes unsuccessfully
    // (cancellation or fault) so a later WarmupAsync can retry the load.
    // (Copilot reviews on PR #1013 and #1016.)
    private Task? _warmupTask;

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
    /// Gets the database provider ID for tracking. The provider-ID cache is
    /// populated once per process: by <see cref="WarmupAsync"/> at startup
    /// when the warmup hosted service is wired, otherwise by the first
    /// GetProviderId call via a synchronous cold-start fallback inside
    /// <c>EnsureProviderIdCacheLoaded</c>. Subsequent calls are an in-memory
    /// dictionary lookup with no DB access. (Copilot review on PR #1013.)
    /// </summary>
    public int GetProviderId(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name must be a non-empty string.", nameof(providerName));
        }

        var cache = EnsureProviderIdCacheLoaded();

        // Provider implementations expose their own DatabaseName so the mapping
        // has a single source of truth and new providers don't require factory edits.
        if (!_providersByKey.TryGetValue(providerName, out var provider))
        {
            throw new ArgumentException(
                $"Unknown STT provider: '{providerName}'. Available: {string.Join(", ", _providersByKey.Keys)}",
                nameof(providerName));
        }

        if (cache.TryGetValue(provider.DatabaseName, out var id))
        {
            return id;
        }

        throw new ArgumentException(
            $"Provider '{providerName}' has DatabaseName '{provider.DatabaseName}' which is not in the providers table. " +
            $"Known rows: {string.Join(", ", cache.Keys)}",
            nameof(providerName));
    }

    /// <summary>
    /// Pre-loads provider IDs from database into the in-memory cache.
    /// <c>SpeechTranscriberFactoryWarmupService</c> calls this during application
    /// startup, so the hot path (<see cref="GetProviderId"/>) never has to trigger
    /// the synchronous fallback below.
    /// </summary>
    public Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _providerIdCache) != null) return Task.CompletedTask;

        // Share the in-flight warmup across concurrent callers so exactly one
        // DB round-trip fires while a warmup is running. Per-caller
        // cancellation is honoured via WaitAsync — the underlying warmup keeps
        // running so a cancelling caller doesn't abort the shared load for
        // siblings. (Copilot review on PR #1016.)
        Task warmup;
        lock (_cacheLock)
        {
            if (Volatile.Read(ref _providerIdCache) != null) return Task.CompletedTask;
            _warmupTask ??= LoadAndPublishAsync();
            warmup = _warmupTask;
        }

        return cancellationToken.CanBeCanceled
            ? warmup.WaitAsync(cancellationToken)
            : warmup;
    }

    private async Task LoadAndPublishAsync()
    {
        try
        {
            var providers = await _queryProcessor
                .ProcessAsync(new GetProvidersByTypeQuery("stt"), CancellationToken.None)
                .ConfigureAwait(false);

            Dictionary<string, int> published;
            lock (_cacheLock)
            {
                var existing = Volatile.Read(ref _providerIdCache);
                if (existing != null)
                {
                    published = existing;
                }
                else
                {
                    published = providers.ToDictionary(
                        p => p.Name,
                        p => p.Id,
                        StringComparer.OrdinalIgnoreCase);
                    Volatile.Write(ref _providerIdCache, published);
                }
            }

            _logger.LogInformation(
                "Warmed {Count} STT providers from database: {Providers}",
                published.Count,
                string.Join(", ", published.Select(p => $"{p.Key}={p.Value}")));
        }
        catch
        {
            // Reset so a later WarmupAsync or GetProviderId can retry. Without
            // this a transient DB failure would pin the factory to the sync
            // cold-start path for the process lifetime. (Copilot review on
            // PR #1016.)
            lock (_cacheLock)
            {
                if (Volatile.Read(ref _providerIdCache) == null)
                {
                    _warmupTask = null;
                }
            }
            throw;
        }
    }

    /// <summary>
    /// Loads provider IDs from database into cache via synchronous blocking.
    /// In normal production operation this path is not reached because the
    /// warmup hosted service populates the cache at startup before any
    /// transcription pipeline runs; it remains as a defensive fallback for
    /// cases where <see cref="WarmupAsync"/> was not invoked or did not
    /// complete successfully. The sync-over-async is acceptable on that
    /// cold-start path because the caller is inside a double-checked lock
    /// and the blocking call happens at most once per process lifetime.
    /// </summary>
    private Dictionary<string, int> EnsureProviderIdCacheLoaded()
    {
        var existing = Volatile.Read(ref _providerIdCache);
        if (existing != null) return existing;

        // If WarmupAsync is in flight, wait for it instead of issuing a
        // second DB round-trip. Read _warmupTask under the lock (publication
        // via Volatile doesn't apply to non-null task references assigned
        // inside the lock) but await outside the lock. (Copilot review on
        // PR #1016.)
        Task? warmup;
        lock (_cacheLock)
        {
            warmup = _warmupTask;
        }

        if (warmup != null)
        {
            try
            {
                warmup.GetAwaiter().GetResult();
                existing = Volatile.Read(ref _providerIdCache);
                if (existing != null) return existing;
            }
            catch
            {
                // Warmup failed; fall through to the sync cold-start below.
            }
        }

        lock (_cacheLock)
        {
            existing = Volatile.Read(ref _providerIdCache);
            if (existing != null) return existing;

            var providers = _queryProcessor.ProcessAsync(
                new GetProvidersByTypeQuery("stt"), CancellationToken.None).GetAwaiter().GetResult();

            var loaded = providers.ToDictionary(
                p => p.Name,
                p => p.Id,
                StringComparer.OrdinalIgnoreCase);
            Volatile.Write(ref _providerIdCache, loaded);

            _logger.LogInformation(
                "Loaded {Count} STT providers from database (cold-start fallback): {Providers}",
                loaded.Count,
                string.Join(", ", loaded.Select(p => $"{p.Key}={p.Value}")));

            return loaded;
        }
    }
}
