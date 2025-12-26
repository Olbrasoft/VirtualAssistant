using Microsoft.Extensions.DependencyInjection;
using VirtualAssistant.Core.Logging;
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
        // Logging infrastructure
        services.AddSingleton<ICorrelationIdProvider, CorrelationIdProvider>();

        // Core services
        services.AddScoped<INotificationService, NotificationService>();

        return services;
    }
}
