using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Olbrasoft.LinuxDesktop.Core.Models;
using Olbrasoft.LinuxDesktop.Core.Services;
using Olbrasoft.LinuxDesktop.DBus.Services;
using Olbrasoft.VirtualAssistant.Desktop.Configuration;

namespace Olbrasoft.VirtualAssistant.Desktop.Extensions;

/// <summary>
/// Dependency injection extensions for desktop monitoring services.
/// </summary>
public static class DesktopServiceExtensions
{
    /// <summary>
    /// Add desktop monitoring services to the service collection.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddDesktopMonitoring(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<DesktopMonitoringOptions>(
            configuration.GetSection(DesktopMonitoringOptions.SectionName));

        var options = configuration
            .GetSection(DesktopMonitoringOptions.SectionName)
            .Get<DesktopMonitoringOptions>() ?? new DesktopMonitoringOptions();

        if (!options.Enabled)
        {
            return services;
        }

        // Register DesktopMonitorBackgroundService (event-driven D-Bus monitoring)
        // Uses BackgroundService pattern with stable thread context for D-Bus connection
        // Registered as singleton implementing both the interface and concrete type
        // Also registered as hosted service for auto-start
        services.AddSingleton<Services.DesktopMonitorBackgroundService>();
        services.AddSingleton<Services.IDesktopMonitorBackgroundService>(sp =>
            sp.GetRequiredService<Services.DesktopMonitorBackgroundService>());
        services.AddHostedService(sp => sp.GetRequiredService<Services.DesktopMonitorBackgroundService>());

        // Register IdleService (still useful for idle detection)
        services.AddSingleton<IIdleService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<IdleMonitorService>>();
            try
            {
                // Synchronous initialization safe in DI registration context
                return IdleMonitorService.CreateAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                if (options.GracefulDegradation)
                {
                    logger.LogWarning(ex,
                        "Failed to initialize IdleMonitorService. Idle detection will be unavailable.");
                    return new NullIdleService();
                }
                throw;
            }
        });

        // Register DesktopContextService (subscribes to DesktopMonitorBackgroundService events)
        services.AddSingleton<VirtualAssistant.Core.Services.IDesktopContextService>(sp =>
        {
            var monitor = sp.GetService<Services.IDesktopMonitorBackgroundService>();
            var logger = sp.GetRequiredService<ILogger<Services.DesktopContextService>>();
            return new Services.DesktopContextService(monitor, logger);
        });

        // Configure context mapping
        services.Configure<Configuration.ContextMappingOptions>(
            configuration.GetSection(Configuration.ContextMappingOptions.SectionName));

        // Register ContextPromptSelector
        services.AddSingleton<VirtualAssistant.Core.Services.IContextPromptSelector, Services.ContextPromptSelector>();

        // Configure notification filtering
        services.Configure<Configuration.NotificationFilteringOptions>(
            configuration.GetSection(Configuration.NotificationFilteringOptions.SectionName));

        // Register NotificationFilter
        services.AddSingleton<VirtualAssistant.Core.Services.INotificationFilter, Services.ContextAwareNotificationFilter>();

        return services;
    }

    /// <summary>
    /// Null object pattern implementation for IIdleService when D-Bus is unavailable.
    /// </summary>
    private class NullIdleService : IIdleService
    {
        public Task<ulong> GetIdleTimeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0UL);

        public Task<TimeSpan> GetIdleTimeSpanAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(TimeSpan.Zero);

        public Task<bool> IsIdleForAsync(TimeSpan duration, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
