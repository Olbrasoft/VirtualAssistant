using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Data.Queries.ProviderQueries;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Factory for creating and managing speech transcriber instances.
/// Caches provider IDs from database for efficient tracking.
/// </summary>
public class SpeechTranscriberFactory : ISpeechTranscriberFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IQueryProcessor _queryProcessor;
    private readonly SpeechProviderSettings _settings;
    private readonly ILogger<SpeechTranscriberFactory> _logger;

    // Cache for provider IDs - loaded from database ONCE at first use
    private Dictionary<string, int>? _providerIdCache;
    private readonly object _cacheLock = new();

    /// <summary>
    /// Initializes a new instance of <see cref="SpeechTranscriberFactory"/>.
    /// </summary>
    public SpeechTranscriberFactory(
        IServiceProvider serviceProvider,
        IQueryProcessor queryProcessor,
        IOptions<SpeechProviderSettings> settings,
        ILogger<SpeechTranscriberFactory> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _queryProcessor = queryProcessor ?? throw new ArgumentNullException(nameof(queryProcessor));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
    /// </summary>
    public ISpeechTranscriber? GetProvider(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "google" => _serviceProvider.GetKeyedService<ISpeechTranscriber>("google"),
            "whisper" => _serviceProvider.GetService<WhisperSpeechTranscriber>(),
            _ => null
        };
    }

    /// <summary>
    /// Gets all available provider names.
    /// </summary>
    public IEnumerable<string> GetAvailableProviders()
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
    private void EnsureProviderIdCacheLoaded()
    {
        if (_providerIdCache != null) return;

        lock (_cacheLock)
        {
            if (_providerIdCache != null) return;

            // Load all STT providers from database - THIS IS THE ONLY DB QUERY
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
