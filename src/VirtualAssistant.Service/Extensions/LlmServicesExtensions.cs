using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for registering LLM (Large Language Model) router services.
/// </summary>
public static class LlmServicesExtensions
{
    /// <summary>
    /// Adds LLM router services with multi-provider support.
    /// </summary>
    public static IServiceCollection AddLlmServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Prompt loader for LLM routers - upgraded to HybridPromptLoader (file + embedded resource fallback)
        services.AddSingleton<IPromptLoader>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<HybridPromptLoader>>();
            var embeddedLoader = new EmbeddedPromptLoader();
            var promptsPath = Path.Combine(AppContext.BaseDirectory, "Prompts");

            return new HybridPromptLoader(
                promptsPath,
                embeddedLoader,
                logger);
        });

        // HttpClient
        services.AddHttpClient();

        return services;
    }
}
