using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for LLM router services.
/// </summary>
public static class LlmServiceExtensions
{
    /// <summary>
    /// Adds LLM router services.
    /// </summary>
    public static IServiceCollection AddLlmServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Prompt loader for LLM routers
        services.AddSingleton<IPromptLoader, PromptLoader>();

        // HttpClient
        services.AddHttpClient();

        // LLM Routers - register as BaseLlmRouterService for MultiProvider to collect
        services.AddSingleton<BaseLlmRouterService, CerebrasRouterService>();
        services.AddSingleton<BaseLlmRouterService, GroqRouterService>();
        services.AddSingleton<BaseLlmRouterService, MistralRouterService>();
        services.AddSingleton<ILlmRouterService, MultiProviderLlmRouter>();

        return services;
    }
}
