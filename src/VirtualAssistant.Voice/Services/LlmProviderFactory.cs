using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Factory for creating and selecting LLM providers.
/// Supports thread-safe runtime provider switching via configuration.
/// </summary>
public class LlmProviderFactory : ILlmProviderFactory
{
    private readonly ILogger<LlmProviderFactory> _logger;
    private readonly Dictionary<string, ILlmProvider> _providers;
    private readonly IReadOnlyCollection<string> _availableProviders;
    private readonly object _lock = new();
    private volatile string _activeProviderName;

    public LlmProviderFactory(
        IEnumerable<ILlmProvider> providers,
        IOptions<LlmProviderOptions> options,
        ILogger<LlmProviderFactory> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(options);

        // Build provider dictionary with duplicate detection
        _providers = new Dictionary<string, ILlmProvider>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in providers)
        {
            var providerName = provider.ProviderName;

            if (_providers.ContainsKey(providerName))
            {
                throw new ArgumentException(
                    $"Duplicate LLM provider name '{providerName}'. Providers must have unique ProviderName values (case-insensitive).",
                    nameof(providers));
            }

            _providers[providerName] = provider;
        }

        // Cache available providers list (immutable after construction)
        _availableProviders = _providers.Keys.ToList().AsReadOnly();

        _activeProviderName = options.Value.ActiveProvider;

        // Validate configured provider exists
        if (!_providers.ContainsKey(_activeProviderName))
        {
            var availableProviders = string.Join(", ", _providers.Keys);
            _logger.LogWarning(
                "Configured active provider '{ConfiguredProvider}' not found. Available providers: {AvailableProviders}. " +
                "Falling back to first available provider.",
                _activeProviderName,
                availableProviders);

            _activeProviderName = _providers.Keys.FirstOrDefault() ?? string.Empty;
        }

        _logger.LogInformation(
            "LlmProviderFactory initialized with active provider '{ActiveProvider}'. Available providers: {AvailableProviders}",
            _activeProviderName,
            string.Join(", ", _providers.Keys));
    }

    /// <inheritdoc />
    public ILlmProvider GetActiveProvider()
    {
        var activeProviderName = _activeProviderName;

        if (string.IsNullOrEmpty(activeProviderName) || !_providers.TryGetValue(activeProviderName, out var provider))
        {
            throw new InvalidOperationException(
                $"No active LLM provider configured. Available providers: {string.Join(", ", _providers.Keys)}");
        }

        return provider;
    }

    /// <inheritdoc />
    public ILlmProvider? GetProvider(string providerName)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);

        _providers.TryGetValue(providerName, out var provider);
        return provider;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetAvailableProviders()
    {
        return _availableProviders;
    }

    /// <inheritdoc />
    public bool SetActiveProvider(string providerName)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);

        lock (_lock)
        {
            if (!_providers.ContainsKey(providerName))
            {
                _logger.LogWarning(
                    "Cannot set active provider to '{ProviderName}' - provider not found. Available: {AvailableProviders}",
                    providerName,
                    string.Join(", ", _providers.Keys));
                return false;
            }

            var previousProvider = _activeProviderName;
            _activeProviderName = providerName;

            _logger.LogInformation(
                "Active LLM provider changed from '{PreviousProvider}' to '{NewProvider}'",
                previousProvider,
                providerName);

            return true;
        }
    }
}
