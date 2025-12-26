using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Service.Workers;
using Olbrasoft.VirtualAssistant.Voice;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for background worker services.
/// </summary>
public static class BackgroundWorkerExtensions
{
    /// <summary>
    /// Adds background worker services.
    /// </summary>
    public static IServiceCollection AddBackgroundWorkers(this IServiceCollection services)
    {
        services.AddHostedService<KeyboardMonitorWorker>();
        services.AddHostedService<ContinuousListenerWorker>();

        // Startup notification (Phase 1: simple "System started")
        services.AddHostedService<StartupNotificationService>();

        return services;
    }
}
