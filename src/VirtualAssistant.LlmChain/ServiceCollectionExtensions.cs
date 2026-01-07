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
            foreach (var provider in options.Providers)
            {
                // Skip if provider already has API keys from file or inline
                if (provider.ApiKeys.Count > 0 || !string.IsNullOrEmpty(provider.ApiKeysFile))
                    continue;

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
}
