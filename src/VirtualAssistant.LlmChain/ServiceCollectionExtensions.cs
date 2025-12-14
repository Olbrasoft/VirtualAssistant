using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtualAssistant.LlmChain.Configuration;

namespace VirtualAssistant.LlmChain;

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
