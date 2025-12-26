namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Main extension methods for VirtualAssistant service registration.
/// </summary>
public static class VirtualAssistantServiceExtensions
{
    /// <summary>
    /// Adds all VirtualAssistant services to the service collection.
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
