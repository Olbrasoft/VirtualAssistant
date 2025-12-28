namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Main orchestrator for VirtualAssistant service registration.
/// Delegates to focused extension classes for each domain.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all VirtualAssistant services to the service collection.
    /// Orchestrates registration across all domain-specific extension methods.
    /// </summary>
    public static IServiceCollection AddVirtualAssistantServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddCoreConfiguration(configuration)
            .AddDataServices(configuration)
            .AddVoiceServices(configuration)
            .AddTtsServices(configuration)
            .AddLlmServices(configuration)
            .AddTrayServices()
            .AddBackgroundWorkers()
            .AddControllers();

        return services;
    }
}
