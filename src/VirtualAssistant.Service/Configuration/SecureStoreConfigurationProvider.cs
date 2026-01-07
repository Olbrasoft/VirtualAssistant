using NeoSmart.SecureStore;

namespace Olbrasoft.VirtualAssistant.Service.Configuration;

/// <summary>
/// Configuration provider that loads secrets from SecureStore encrypted vault.
/// Secrets are loaded into IConfiguration and can be accessed by key name.
/// </summary>
public class SecureStoreConfigurationProvider : ConfigurationProvider
{
    private readonly string _secretsPath;
    private readonly string _keyPath;
    private readonly ILogger<SecureStoreConfigurationProvider>? _logger;

    /// <summary>
    /// Initializes a new instance of SecureStoreConfigurationProvider.
    /// </summary>
    /// <param name="secretsPath">Path to secrets.json vault file.</param>
    /// <param name="keyPath">Path to secrets.key file.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public SecureStoreConfigurationProvider(string secretsPath, string keyPath, ILogger<SecureStoreConfigurationProvider>? logger = null)
    {
        _secretsPath = secretsPath;
        _keyPath = keyPath;
        _logger = logger;
    }

    /// <summary>
    /// Loads secrets from SecureStore vault into configuration.
    /// </summary>
    public override void Load()
    {
        if (!File.Exists(_secretsPath))
        {
            _logger?.LogWarning("SecureStore vault not found at {Path}, skipping", _secretsPath);
            return;
        }

        if (!File.Exists(_keyPath))
        {
            _logger?.LogWarning("SecureStore key file not found at {Path}, skipping", _keyPath);
            return;
        }

        try
        {
            using var secrets = SecretsManager.LoadStore(_secretsPath);
            secrets.LoadKeyFromFile(_keyPath);

            var loadedCount = 0;
            foreach (var key in secrets.Keys)
            {
                Data[key] = secrets.Get(key);
                loadedCount++;
            }

            _logger?.LogInformation("SecureStore loaded {Count} secrets from {Path}", loadedCount, _secretsPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load SecureStore vault from {Path}", _secretsPath);
            // Don't throw - allow application to start without secrets (will fail later when secret is needed)
        }
    }
}

/// <summary>
/// Configuration source for SecureStore provider.
/// </summary>
public class SecureStoreConfigurationSource : IConfigurationSource
{
    /// <summary>
    /// Gets or sets the path to secrets.json vault file.
    /// </summary>
    public string SecretsPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to secrets.key file.
    /// </summary>
    public string KeyPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional logger factory.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <inheritdoc />
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        var logger = LoggerFactory?.CreateLogger<SecureStoreConfigurationProvider>();
        return new SecureStoreConfigurationProvider(SecretsPath, KeyPath, logger);
    }
}

/// <summary>
/// Extension methods for adding SecureStore configuration source.
/// </summary>
public static class SecureStoreConfigurationExtensions
{
    /// <summary>
    /// Adds SecureStore vault as a configuration source.
    /// Secrets from the vault will be available via IConfiguration.
    /// </summary>
    /// <param name="builder">Configuration builder.</param>
    /// <param name="secretsPath">Path to secrets.json vault file.</param>
    /// <param name="keyPath">Path to secrets.key file.</param>
    /// <param name="loggerFactory">Optional logger factory for diagnostics.</param>
    /// <returns>The configuration builder for chaining.</returns>
    public static IConfigurationBuilder AddSecureStore(
        this IConfigurationBuilder builder,
        string secretsPath,
        string keyPath,
        ILoggerFactory? loggerFactory = null)
    {
        // Expand ~ to home directory
        secretsPath = ExpandPath(secretsPath);
        keyPath = ExpandPath(keyPath);

        return builder.Add(new SecureStoreConfigurationSource
        {
            SecretsPath = secretsPath,
            KeyPath = keyPath,
            LoggerFactory = loggerFactory
        });
    }

    private static string ExpandPath(string path)
    {
        if (path.StartsWith('~'))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path[2..]); // Skip "~/"
        }
        return path;
    }
}
