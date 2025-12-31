using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Configuration;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Service.Tray;
using Olbrasoft.VirtualAssistant.Service.Tray.Menu;
using Olbrasoft.VirtualAssistant.Service.Workers;
using Olbrasoft.VirtualAssistant.Voice;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.SystemTray.Linux;
using SystemTrayMenuHandler = Olbrasoft.SystemTray.Linux.ITrayMenuHandler;

namespace Olbrasoft.VirtualAssistant.Service.Extensions;

/// <summary>
/// Extension methods for registering system tray icon services.
/// </summary>
public static class TrayServicesExtensions
{
    /// <summary>
    /// Adds system tray icon services and D-Bus menu handlers.
    /// </summary>
    public static IServiceCollection AddTrayServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Service monitoring configuration
        services.Configure<ServiceMonitoringOptions>(
            configuration.GetSection(ServiceMonitoringOptions.SectionName));

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

        // Adapter for integrating the system tray icon manager
        services.AddSingleton<Core.Services.ITrayIconManager>(sp =>
        {
            var manager = sp.GetRequiredService<TrayIconManager>();
            return new SystemTrayIconManagerAdapter(manager);
        });

        // Coordinator for managing tray icon behavior
        services.AddSingleton<ITrayIconCoordinator>(sp =>
        {
            var manager = sp.GetRequiredService<Core.Services.ITrayIconManager>();
            var iconsPath = Path.Combine(AppContext.BaseDirectory, "icons");
            var muteService = sp.GetRequiredService<IManualMuteService>();
            var logger = sp.GetRequiredService<ILogger<TrayIconCoordinator>>();
            var menuHandler = sp.GetRequiredService<SystemTrayMenuHandler>();
            return new TrayIconCoordinator(manager, iconsPath, muteService, logger, menuHandler as Core.Services.ITrayMenuHandler);
        });

        // Menu state manager (issue #468 - SRP refactoring)
        services.AddSingleton<IMenuStateManager, MenuStateManager>();

        // Menu layout builder (issue #468 - SRP refactoring)
        services.AddSingleton<IMenuLayoutBuilder, MenuLayoutBuilder>();

        // Menu event router (issue #468 - SRP refactoring)
        services.AddSingleton<IMenuEventRouter, MenuEventRouter>();

        // D-Bus menu handler for tray icon context menu (facade pattern)
        services.AddSingleton<SystemTrayMenuHandler>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<VirtualAssistantDBusMenuHandler>>();
            var stateManager = sp.GetRequiredService<IMenuStateManager>();
            var layoutBuilder = sp.GetRequiredService<IMenuLayoutBuilder>();
            var eventRouter = sp.GetRequiredService<IMenuEventRouter>();
            return new VirtualAssistantDBusMenuHandler(logger, stateManager, layoutBuilder, eventRouter);
        });

        // NOTE: SpeechToText service manager removed (issue #466) - STT runs inline now

        // Service lifecycle manager for managing dependent services
        services.AddSingleton<IServiceLifecycleManager>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ServiceLifecycleManager>>();
            var options = sp.GetRequiredService<IOptions<ServiceMonitoringOptions>>();
            var menuHandler = sp.GetRequiredService<SystemTrayMenuHandler>();
            return new ServiceLifecycleManager(logger, options, menuHandler as IServiceStatusUpdater);
        });

        // Icon animation service for hand icon animations
        services.AddSingleton<IIconAnimationService, IconAnimationService>();

        // Menu event dispatcher for handling tray menu actions
        services.AddSingleton<IMenuEventDispatcher>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<MenuEventDispatcher>>();
            var muteService = sp.GetRequiredService<IManualMuteService>();
            var settingsService = sp.GetRequiredService<ISettingsService>();
            var options = sp.GetRequiredService<IOptions<ContinuousListenerOptions>>();
            var llmProvider = sp.GetService<ILlmProvider>();
            var dictationControl = sp.GetService<IDictationControl>();

            return new MenuEventDispatcher(
                logger,
                muteService,
                settingsService,
                options.Value.LogViewerPort,
                llmProvider,
                dictationControl);
        });

        // State notification handler for state synchronization
        services.AddSingleton<IStateNotificationHandler>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<StateNotificationHandler>>();
            var muteService = sp.GetRequiredService<IManualMuteService>();
            var settingsService = sp.GetRequiredService<ISettingsService>();
            var menuHandler = sp.GetRequiredService<SystemTrayMenuHandler>();
            var iconCoordinator = sp.GetRequiredService<ITrayIconCoordinator>();
            var iconAnimationService = sp.GetRequiredService<IIconAnimationService>();
            var lifecycleManager = sp.GetService<IServiceLifecycleManager>();
            var dictationStateMachine = sp.GetService<Olbrasoft.VirtualAssistant.Voice.StateMachine.IDictationStateMachine>();
            var dictationWorker = sp.GetService<DictationWorker>();

            return new StateNotificationHandler(
                logger,
                muteService,
                settingsService,
                (IServiceStatusUpdater)menuHandler,
                iconCoordinator,
                iconAnimationService,
                lifecycleManager,
                dictationStateMachine,
                dictationWorker);
        });

        // Tray coordinator service (orchestrates 5 specialized tray services)
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<TrayCoordinatorService>>();
            var iconCoordinator = sp.GetRequiredService<ITrayIconCoordinator>();
            var menuDispatcher = sp.GetRequiredService<IMenuEventDispatcher>();
            var lifecycleManager = sp.GetRequiredService<IServiceLifecycleManager>();
            var stateHandler = sp.GetRequiredService<IStateNotificationHandler>();
            var iconAnimationService = sp.GetRequiredService<IIconAnimationService>();
            var menuHandler = sp.GetRequiredService<SystemTrayMenuHandler>();

            return new TrayCoordinatorService(
                logger,
                iconCoordinator,
                menuDispatcher,
                lifecycleManager,
                stateHandler,
                iconAnimationService,
                menuHandler);
        });

        return services;
    }
}
