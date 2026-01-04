using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Data;
using Olbrasoft.VirtualAssistant.Data.EntityFrameworkCore;
using Olbrasoft.VirtualAssistant.GitHub;
using Olbrasoft.VirtualAssistant.Core;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for registering data layer services (EF Core, repositories).
/// </summary>
public static class DataServicesExtensions
{
    /// <summary>
    /// Adds data layer services including DbContext, repositories, and GitHub sync.
    /// </summary>
    public static IServiceCollection AddDataServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database connection
        var connectionString = configuration.GetConnectionString("VirtualAssistantDb")
            ?? throw new InvalidOperationException("Connection string 'VirtualAssistantDb' not found.");
        services.AddVirtualAssistantData(connectionString);

        // CQRS - Register Query/Command handlers from VirtualAssistant.Data.EntityFrameworkCore assembly
        services.AddCqrs(ServiceLifetime.Scoped, typeof(VirtualAssistantDbContext).Assembly);

        // GitHub sync services
        services.AddGitHubServices(configuration);

        // Core services (AgentHubService)
        services.AddCoreServices();

        return services;
    }
}
