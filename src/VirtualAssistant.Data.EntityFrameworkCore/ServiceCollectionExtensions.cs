using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Olbrasoft.Data.Cqrs;

namespace VirtualAssistant.Data.EntityFrameworkCore;

/// <summary>
/// Extension methods for registering VirtualAssistant data services in DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds VirtualAssistant DbContext and CQRS handlers to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="lifetime">Service lifetime for CQRS handlers (default: Scoped).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVirtualAssistantData(
        this IServiceCollection services,
        string connectionString,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        // Register DbContext with PostgreSQL
        services.AddDbContext<VirtualAssistantDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        }, lifetime);

        // Register CQRS handlers from this assembly
        services.AddCqrs(lifetime, typeof(VirtualAssistantDbContext).Assembly);

        return services;
    }
}
