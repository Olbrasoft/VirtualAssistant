using Microsoft.Extensions.Options;
using Olbrasoft.SystemTray.Linux;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Tray;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for tray icon services.
/// </summary>
public static class TrayServiceExtensions
{
    /// <summary>
    /// Adds tray icon services.
    /// </summary>
    public static IServiceCollection AddTrayServices(this IServiceCollection services)
    {
        // Icon renderer for SVG rendering
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<IconRenderer>>();
            return new IconRenderer(logger);
        });

        // Tray icon manager for managing tray icons
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<TrayIconManager>>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var iconRenderer = sp.GetRequiredService<IconRenderer>();
            return new TrayIconManager(logger, loggerFactory, iconRenderer);
        });

        // D-Bus menu handler for tray icon context menu
        services.AddSingleton<ITrayMenuHandler>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<VirtualAssistantDBusMenuHandler>>();
            return new VirtualAssistantDBusMenuHandler(logger);
        });

        // VirtualAssistant tray service (inline lambda registration)
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<VirtualAssistantTrayService>>();
            var manager = sp.GetRequiredService<TrayIconManager>();
            var muteService = sp.GetRequiredService<IManualMuteService>();
            var menuHandler = sp.GetRequiredService<ITrayMenuHandler>();
            var options = sp.GetRequiredService<IOptions<ContinuousListenerOptions>>();

            var iconsPath = Path.Combine(AppContext.BaseDirectory, "icons");

            return new VirtualAssistantTrayService(
                logger,
                manager,
                muteService,
                iconsPath,
                options.Value.LogViewerPort,
                menuHandler);
        });

        return services;
    }
}
