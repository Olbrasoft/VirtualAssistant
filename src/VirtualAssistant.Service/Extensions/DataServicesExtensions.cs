using Olbrasoft.Data.Cqrs;
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
        // Database connection - build from separate config values (password from SecureStore)
        var connectionString = BuildConnectionString(configuration);
        services.AddVirtualAssistantData(connectionString);

        // CQRS - Register Query/Command handlers from VirtualAssistant.Data.EntityFrameworkCore assembly
        services.AddCqrs(ServiceLifetime.Scoped, typeof(VirtualAssistantDbContext).Assembly);

        // GitHub sync services
        services.AddGitHubServices(configuration);

        // Core services (AgentHubService)
        services.AddCoreServices();

        return services;
    }

    /// <summary>
    /// Builds PostgreSQL connection string from separate configuration values.
    /// Password is loaded from SecureStore for security.
    /// </summary>
    private static string BuildConnectionString(IConfiguration configuration)
    {
        var host = configuration["Database:Host"] ?? "localhost";
        var database = configuration["Database:Name"] ?? "virtual_assistant";
        var username = configuration["Database:Username"] ?? "postgres";
        var password = configuration["Database:Password"]
            ?? throw new InvalidOperationException("Database:Password not found in configuration. Add it to SecureStore.");

        return $"Host={host};Database={database};Username={username};Password={password}";
    }
}
