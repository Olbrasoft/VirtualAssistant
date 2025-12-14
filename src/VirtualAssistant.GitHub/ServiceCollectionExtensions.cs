using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtualAssistant.GitHub.Configuration;
using VirtualAssistant.GitHub.Services;

namespace VirtualAssistant.GitHub;

/// <summary>
/// Extension methods for registering GitHub services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds GitHub services to the service collection.
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

        // Register issue status service (for orphaned task detection)
        services.AddScoped<IGitHubIssueStatusService, GitHubIssueStatusService>();

        // Register reference service (for ensuring GitHub references exist in database)
        services.AddScoped<IGitHubReferenceService, GitHubReferenceService>();

        return services;
    }
}
