using VirtualAssistant.Core;
using VirtualAssistant.Data.EntityFrameworkCore;
using VirtualAssistant.GitHub;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for data layer services (EF Core, CQRS handlers).
/// </summary>
public static class DataServiceExtensions
{
    /// <summary>
    /// Adds data layer services (EF Core, CQRS handlers).
    /// </summary>
    public static IServiceCollection AddDataServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("VirtualAssistantDb")
            ?? throw new InvalidOperationException("Connection string 'VirtualAssistantDb' not found.");

        services.AddVirtualAssistantData(connectionString);

        // GitHub sync services
        services.AddGitHubServices(configuration);

        // Core services (AgentHubService)
        services.AddCoreServices();

        return services;
    }
}
