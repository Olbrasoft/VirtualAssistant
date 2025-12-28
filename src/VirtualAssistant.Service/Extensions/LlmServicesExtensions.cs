using Olbrasoft.VirtualAssistant.Voice;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Voice.Workers;
using VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for LLM router services registration.
/// Handles multi-provider LLM routing for text processing.
/// </summary>
public static class LlmServicesExtensions
{
    /// <summary>
    /// Adds LLM router services.
    /// </summary>
    public static IServiceCollection AddLlmServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Prompt loader for LLM routers - upgraded to HybridPromptLoader (file + embedded resource fallback)
        services.AddSingleton<Olbrasoft.VirtualAssistant.Voice.Services.IPromptLoader>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Olbrasoft.VirtualAssistant.Voice.Services.HybridPromptLoader>>();
            var embeddedLoader = new Olbrasoft.VirtualAssistant.Voice.Services.EmbeddedPromptLoader();
            var promptsPath = Path.Combine(AppContext.BaseDirectory, "Prompts");

            return new Olbrasoft.VirtualAssistant.Voice.Services.HybridPromptLoader(
                promptsPath,
                embeddedLoader,
                logger);
        });

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
