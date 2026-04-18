using Olbrasoft.VirtualAssistant.Desktop.Extensions;
using Olbrasoft.VirtualAssistant.Service.Hubs.Services;

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

        // Razor Pages for Admin Dashboard
        services.AddRazorPages();

        // SignalR for Desktop Monitor real-time communication
        services.AddSignalR();

        // Remote Control hub domain services (#970 split — each slice has
        // ≤5 ctor deps and focuses on one concern so the hub itself stays a
        // wire-protocol facade).
        services.AddSingleton<IRemoteRecordingCommands, RemoteRecordingCommands>();
        services.AddSingleton<IRemoteDesktopCommands, RemoteDesktopCommands>();
        services.AddSingleton<IRemoteScreenshotCommands, RemoteScreenshotCommands>();

        return services;
    }
}
