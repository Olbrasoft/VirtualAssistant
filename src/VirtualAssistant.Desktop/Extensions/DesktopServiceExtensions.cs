using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Olbrasoft.LinuxDesktop.Core.Models;
using Olbrasoft.LinuxDesktop.Core.Services;
using Olbrasoft.LinuxDesktop.DBus.Services;
using VirtualAssistant.Desktop.Configuration;

namespace VirtualAssistant.Desktop.Extensions;

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

        // Register FocusTrackerService (focus-tracker GNOME extension integration)
        // Uses lazy initialization - D-Bus connection created on first use
#pragma warning disable CS8634 // Nullable type constraint warning - DesktopContextService handles null
        services.AddSingleton<Services.IFocusTrackerService?>(sp =>
#pragma warning restore CS8634
        {
            var logger = sp.GetRequiredService<ILogger<Services.FocusTrackerService>>();

            if (!options.GracefulDegradation)
            {
                // No graceful degradation - let initialization errors propagate
                return new Services.FocusTrackerService(logger);
            }

            // With graceful degradation - return instance even if extension unavailable
            // Connection errors will be logged on first use
            return new Services.FocusTrackerService(logger);
        });

        // Register IdleService (still useful for idle detection)
        services.AddSingleton<IIdleService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<IdleMonitorService>>();
            try
            {
                // Safe in console app context (see FocusTrackerService comment above)
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

        // Register DesktopContextService (uses FocusTrackerService)
        services.AddSingleton<VirtualAssistant.Core.Services.IDesktopContextService, Services.DesktopContextService>();

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
