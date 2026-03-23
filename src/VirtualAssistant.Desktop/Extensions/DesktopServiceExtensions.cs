using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        services.AddSingleton<Core.Services.IDesktopContextService>(sp =>
        {
            var monitor = sp.GetService<Services.IDesktopMonitorBackgroundService>();
            var logger = sp.GetRequiredService<ILogger<Services.DesktopContextService>>();
            return new Services.DesktopContextService(monitor, logger);
        });

        // Configure notification filtering
        services.Configure<NotificationFilteringOptions>(
            configuration.GetSection(Configuration.NotificationFilteringOptions.SectionName));

        // Register NotificationFilter
        services.AddSingleton<Core.Services.INotificationFilter, Services.ContextAwareNotificationFilter>();

        // Register WindowService for remote window activation (issue #854)
        // Uses D-Bus via window-calls GNOME extension (Wayland compatible)
        services.AddSingleton<IWindowService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<WindowService>>();
            try
            {
                return WindowService.CreateAsync(logger).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                if (options.GracefulDegradation)
                {
                    logger.LogWarning(ex,
                        "Failed to initialize WindowService. Window activation will be unavailable.");
                    return new NullWindowService();
                }
                throw;
            }
        });
        services.AddSingleton<IWindowQueryService>(sp => sp.GetRequiredService<IWindowService>());
        services.AddSingleton<IWindowActionService>(sp => sp.GetRequiredService<IWindowService>());

        return services;
    }

    /// <summary>
    /// Null object pattern implementation for IWindowService when D-Bus/GNOME extension is unavailable.
    /// </summary>
    private class NullWindowService : IWindowService
    {
        private static readonly IReadOnlyList<LinuxDesktop.Core.Models.WindowInfo> Empty = [];
        public Task<IReadOnlyList<LinuxDesktop.Core.Models.WindowInfo>> GetWindowsAsync(CancellationToken ct = default) => Task.FromResult(Empty);
        public Task<LinuxDesktop.Core.Models.WindowDetails?> GetWindowDetailsAsync(uint windowId, CancellationToken ct = default) => Task.FromResult<LinuxDesktop.Core.Models.WindowDetails?>(null);
        public Task<LinuxDesktop.Core.Models.WindowInfo?> GetFocusedWindowAsync(CancellationToken ct = default) => Task.FromResult<LinuxDesktop.Core.Models.WindowInfo?>(null);
        public Task<string?> GetWindowTitleAsync(uint windowId, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task ActivateWindowAsync(uint windowId, CancellationToken ct = default) => Task.CompletedTask;
        public Task CloseWindowAsync(uint windowId, CancellationToken ct = default) => Task.CompletedTask;
        public Task MaximizeWindowAsync(uint windowId, CancellationToken ct = default) => Task.CompletedTask;
        public Task MinimizeWindowAsync(uint windowId, CancellationToken ct = default) => Task.CompletedTask;
        public Task UnmaximizeWindowAsync(uint windowId, CancellationToken ct = default) => Task.CompletedTask;
        public Task UnminimizeWindowAsync(uint windowId, CancellationToken ct = default) => Task.CompletedTask;
        public Task MoveWindowAsync(uint windowId, int x, int y, CancellationToken ct = default) => Task.CompletedTask;
        public Task ResizeWindowAsync(uint windowId, int width, int height, CancellationToken ct = default) => Task.CompletedTask;
        public Task MoveWindowToWorkspaceAsync(uint windowId, int workspaceIndex, CancellationToken ct = default) => Task.CompletedTask;
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
