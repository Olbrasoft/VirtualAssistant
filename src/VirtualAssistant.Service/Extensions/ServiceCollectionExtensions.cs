using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Olbrasoft.VirtualAssistant.Desktop.Extensions;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for service collection configuration.
/// Orchestrates registration of all VirtualAssistant services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all VirtualAssistant services to the service collection.
    /// </summary>
    public static IServiceCollection AddVirtualAssistantServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddCoreServices(configuration)
            .AddDataServices(configuration)
            .AddVoiceServices(configuration)
            .AddTtsServices(configuration)
            .AddLlmServices(configuration)
            .AddTrayServices(configuration)
            .AddDesktopMonitoring(configuration)
            .AddWorkerServices();

        // MVC Controllers
        services.AddControllers();

        // SignalR for Desktop Monitor real-time communication
        services.AddSignalR();

        return services;
    }
}
