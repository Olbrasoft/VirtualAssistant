using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Olbrasoft.VirtualAssistant.LlmChain.Configuration;

namespace Olbrasoft.VirtualAssistant.LlmChain;

/// <summary>
/// Extension methods for registering LlmChain services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the LLM chain client with configuration from appsettings.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration root.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddLlmChain(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<LlmChainOptions>(configuration.GetSection(LlmChainOptions.SectionName));

        // Load API keys from SecureStore configuration paths
        // This allows storing keys in SecureStore instead of external files
        services.PostConfigure<LlmChainOptions>(options =>
        {
            // Filter providers that need API keys from SecureStore
            var providersNeedingKeys = options.Providers
                .Where(p => p.ApiKeys.Count == 0 && !ApiKeysFileExists(p.ApiKeysFile));

            foreach (var provider in providersNeedingKeys)
            {
                // Try to load from SecureStore configuration path: LlmChain:{ProviderName}:ApiKey (single)
                // or LlmChain:{ProviderName}:ApiKeys (comma-separated)
                var singleKey = configuration[$"LlmChain:{provider.Name}:ApiKey"];
                if (!string.IsNullOrEmpty(singleKey))
                {
                    provider.ApiKeys.Add(singleKey);
                    continue;
                }

                var multipleKeys = configuration[$"LlmChain:{provider.Name}:ApiKeys"];
                if (!string.IsNullOrEmpty(multipleKeys))
                {
                    var keys = multipleKeys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    provider.ApiKeys.AddRange(keys);
                }
            }

            // Warn about enabled providers with no API keys configured
            foreach (var provider in options.Providers.Where(p => p.Enabled && p.ApiKeys.Count == 0 && !ApiKeysFileExists(p.ApiKeysFile)))
            {
                Console.Error.WriteLine(
                    $"[LlmChain] Warning: No API keys found for enabled provider '{provider.Name}'. " +
                    "Add keys to SecureStore (LlmChain:{Name}:ApiKey or LlmChain:{Name}:ApiKeys) or disable the provider.");
            }
        });

        services.AddHttpClient();
        services.AddSingleton<ILlmChainClient, LlmChainClient>();

        return services;
    }

    /// <summary>
    /// Adds the LLM chain client with explicit options.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Options configuration action.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddLlmChain(
        this IServiceCollection services,
        Action<LlmChainOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient();
        services.AddSingleton<ILlmChainClient, LlmChainClient>();

        return services;
    }

    /// <summary>
    /// Checks if the API keys file exists at the specified path.
    /// </summary>
    private static bool ApiKeysFileExists(string? apiKeysFile)
    {
        if (string.IsNullOrEmpty(apiKeysFile))
            return false;

        var path = apiKeysFile.StartsWith('~')
            ? apiKeysFile.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
            : apiKeysFile;

        return File.Exists(path);
    }
}
