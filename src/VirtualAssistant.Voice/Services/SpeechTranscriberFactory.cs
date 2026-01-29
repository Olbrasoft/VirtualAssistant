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
    private readonly ISpeechTranscriber _whisperProvider;
    private readonly ISpeechTranscriber _googleProvider;
    private readonly IQueryProcessor _queryProcessor;
    private readonly SpeechProviderSettings _settings;
    private readonly ILogger<SpeechTranscriberFactory> _logger;

    // Cache for provider IDs - loaded from database ONCE at first use
    private Dictionary<string, int>? _providerIdCache;
    private readonly object _cacheLock = new();

    /// <summary>
    /// Initializes a new instance of <see cref="SpeechTranscriberFactory"/>.
    /// Providers are injected explicitly to avoid .NET keyed services bugs.
    /// </summary>
    /// <param name="whisperProvider">The Whisper speech transcriber instance.</param>
    /// <param name="googleProvider">The Google speech transcriber instance.</param>
    /// <param name="queryProcessor">Query processor for database access.</param>
    /// <param name="settings">Speech provider configuration settings.</param>
    /// <param name="logger">Logger instance.</param>
    public SpeechTranscriberFactory(
        ISpeechTranscriber whisperProvider,
        ISpeechTranscriber googleProvider,
        IQueryProcessor queryProcessor,
        IOptions<SpeechProviderSettings> settings,
        ILogger<SpeechTranscriberFactory> logger)
    {
        _whisperProvider = whisperProvider ?? throw new ArgumentNullException(nameof(whisperProvider));
        _googleProvider = googleProvider ?? throw new ArgumentNullException(nameof(googleProvider));
        _queryProcessor = queryProcessor ?? throw new ArgumentNullException(nameof(queryProcessor));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogInformation(
            "SpeechTranscriberFactory initialized with Whisper={Whisper}, Google={Google}",
            _whisperProvider.GetType().Name,
            _googleProvider.GetType().Name);
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
    /// Gets a specific provider by name.
    /// Returns the explicitly injected provider instance - no service locator pattern.
    /// </summary>
    /// <param name="providerName">The provider name ("whisper" or "google").</param>
    /// <returns>The provider instance, or null if not found.</returns>
    public ISpeechTranscriber? GetProvider(string providerName)
    {
        if (string.IsNullOrEmpty(providerName))
            return null;

        var provider = providerName.ToLowerInvariant() switch
        {
            "whisper" => (ISpeechTranscriber)_whisperProvider,
            "google" => _googleProvider,
            _ => null
        };

        if (provider != null)
        {
            _logger.LogDebug(
                "GetProvider({ProviderName}) returning {ProviderType}",
                providerName,
                provider.GetType().Name);
        }

        return provider;
    }

    /// <summary>
    /// Gets all available provider names.
    /// </summary>
    public IReadOnlyCollection<string> GetAvailableProviders()
    {
        return ["whisper", "google"];
    }

    /// <summary>
    /// Gets the database provider ID for tracking.
    /// Returns from cache - loaded once at startup, no DB query per call.
    /// </summary>
    public int GetProviderId(string providerName)
    {
        EnsureProviderIdCacheLoaded();

        // Map friendly names to database names
        var dbName = providerName.ToLowerInvariant() switch
        {
            "whisper" => "Whisper Local",
            "google" => "Google Speech-to-Text",
            _ => providerName
        };

        if (_providerIdCache!.TryGetValue(dbName, out var id))
        {
            return id;
        }

        throw new ArgumentException(
            $"Unknown STT provider: '{providerName}'. Available: {string.Join(", ", _providerIdCache.Keys)}",
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
