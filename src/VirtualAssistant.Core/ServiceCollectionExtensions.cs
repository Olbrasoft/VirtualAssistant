using Microsoft.Extensions.DependencyInjection;
using VirtualAssistant.Core.Services;

namespace VirtualAssistant.Core;

/// <summary>
/// Extension methods for registering VirtualAssistant.Core services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds core services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // Task queue service for agent task management
        services.AddScoped<IAgentTaskService, AgentTaskService>();

        // Background service for automatic task distribution
        services.AddHostedService<TaskDistributionService>();

        return services;
    }
}
