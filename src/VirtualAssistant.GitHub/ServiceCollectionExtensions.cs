using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VirtualAssistant.GitHub.Configuration;
using VirtualAssistant.GitHub.Services;

namespace VirtualAssistant.GitHub;

/// <summary>
/// Extension methods for registering GitHub services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds GitHub sync services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration containing GitHub settings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGitHubServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register GitHub configuration
        services.Configure<GitHubSettings>(
            configuration.GetSection(GitHubSettings.SectionName));

        // Register Embedding configuration
        services.Configure<EmbeddingSettings>(
            configuration.GetSection(EmbeddingSettings.SectionName));

        // Register embedding service (Ollama - local, no API key needed)
        services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>();

        // Register sync service
        services.AddScoped<IGitHubSyncService, GitHubSyncService>();

        // Register search service
        services.AddScoped<IGitHubSearchService, GitHubSearchService>();

        // Register issue status service (for orphaned task detection)
        services.AddScoped<IGitHubIssueStatusService, GitHubIssueStatusService>();

        // Register background sync service as singleton (hosted services must be singletons)
        // Also expose as GitHubSyncBackgroundService for health check access
        services.AddSingleton<GitHubSyncBackgroundService>();
        services.AddHostedService(sp => sp.GetRequiredService<GitHubSyncBackgroundService>());

        return services;
    }
}
