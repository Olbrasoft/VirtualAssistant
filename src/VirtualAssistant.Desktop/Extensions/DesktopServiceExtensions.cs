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

        // Register LinuxDesktop services with graceful degradation
        services.AddSingleton<IWindowService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<WindowService>>();
            try
            {
                return WindowService.CreateAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                if (options.GracefulDegradation)
                {
                    logger.LogWarning(ex,
                        "Failed to initialize WindowService (GNOME extensions missing?). " +
                        "Desktop integration will be limited.");
                    return new NullWindowService();
                }
                throw;
            }
        });

        services.AddSingleton<IWorkspaceService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<WorkspaceService>>();
            try
            {
                return WorkspaceService.CreateAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                if (options.GracefulDegradation)
                {
                    logger.LogWarning(ex,
                        "Failed to initialize WorkspaceService (GNOME extensions missing?). " +
                        "Workspace switching will be unavailable.");
                    return new NullWorkspaceService();
                }
                throw;
            }
        });

        services.AddSingleton<IIdleService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<IdleMonitorService>>();
            try
            {
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

        // Register DesktopContextService
        services.AddSingleton<VirtualAssistant.Core.Services.IDesktopContextService, Services.DesktopContextService>();

        return services;
    }

    /// <summary>
    /// Null object pattern implementation for IWindowService when D-Bus is unavailable.
    /// </summary>
    private class NullWindowService : IWindowService
    {
        // IWindowQueryService
        public Task<IReadOnlyList<WindowInfo>> GetWindowsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WindowInfo>>(Array.Empty<WindowInfo>());

        public Task<WindowDetails?> GetWindowDetailsAsync(uint windowId, CancellationToken cancellationToken = default) =>
            Task.FromResult<WindowDetails?>(null);

        public Task<WindowInfo?> GetFocusedWindowAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<WindowInfo?>(null);

        public Task<string?> GetWindowTitleAsync(uint windowId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        // IWindowActionService
        public Task ActivateWindowAsync(uint windowId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task CloseWindowAsync(uint windowId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MaximizeWindowAsync(uint windowId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MinimizeWindowAsync(uint windowId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UnmaximizeWindowAsync(uint windowId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UnminimizeWindowAsync(uint windowId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        // IWindowLayoutService
        public Task MoveWindowAsync(uint windowId, int x, int y, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ResizeWindowAsync(uint windowId, int width, int height, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        // IWindowWorkspaceService
        public Task MoveWindowToWorkspaceAsync(uint windowId, int workspaceIndex, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Null object pattern implementation for IWorkspaceService when D-Bus is unavailable.
    /// </summary>
    private class NullWorkspaceService : IWorkspaceService
    {
        public Task<IReadOnlyList<WorkspaceInfo>> GetWorkspacesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkspaceInfo>>(Array.Empty<WorkspaceInfo>());

        public Task<int> GetWorkspaceCountAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(1);

        public Task<int> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task SwitchWorkspaceAsync(int index, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<WindowInfo>> GetWorkspaceWindowsAsync(int index, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WindowInfo>>(Array.Empty<WindowInfo>());

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
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
