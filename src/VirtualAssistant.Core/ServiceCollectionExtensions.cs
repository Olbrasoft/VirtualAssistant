using Microsoft.Extensions.DependencyInjection;

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
        // No core services currently registered
        return services;
    }
}
