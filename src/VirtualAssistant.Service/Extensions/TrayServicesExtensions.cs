using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Service.Tray;
using Olbrasoft.VirtualAssistant.Service.Workers;
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

        // D-Bus menu handler for tray icon context menu
        services.AddSingleton<SystemTrayMenuHandler>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<VirtualAssistantDBusMenuHandler>>();
            return new VirtualAssistantDBusMenuHandler(logger);
        });

        // SpeechToText service manager for controlling SpeechToText microservice
        services.AddSingleton<ISpeechToTextServiceManager, SpeechToTextServiceManager>();

        // Service lifecycle manager for managing dependent services
        services.AddSingleton<IServiceLifecycleManager>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ServiceLifecycleManager>>();
            var sttManager = sp.GetService<ISpeechToTextServiceManager>();
            var menuHandler = sp.GetRequiredService<SystemTrayMenuHandler>();
            return new ServiceLifecycleManager(logger, sttManager, menuHandler as IServiceStatusUpdater);
        });

        // Icon animation service for hand icon animations
        services.AddSingleton<IIconAnimationService, IconAnimationService>();

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

        // VirtualAssistant tray service (wrapper for tray functionality)
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<VirtualAssistantTrayService>>();
            var manager = sp.GetRequiredService<TrayIconManager>();
            var muteService = sp.GetRequiredService<IManualMuteService>();
            var settingsService = sp.GetRequiredService<ISettingsService>();
            // NOTE: DependentServicesManager removed - TTS runs inline (issue #407)
            var menuHandler = sp.GetRequiredService<SystemTrayMenuHandler>();
            var sttServiceManager = sp.GetService<ISpeechToTextServiceManager>();
            var mistralProvider = sp.GetService<Olbrasoft.VirtualAssistant.Voice.Services.ILlmProvider>();
            var dictationStateMachine = sp.GetRequiredService<Olbrasoft.VirtualAssistant.Voice.StateMachine.IDictationStateMachine>();
            var dictationWorker = sp.GetRequiredService<DictationWorker>();
            var options = sp.GetRequiredService<IOptions<ContinuousListenerOptions>>();
            var iconsPath = Path.Combine(AppContext.BaseDirectory, "icons");

            return new VirtualAssistantTrayService(
                logger,
                manager,
                muteService,
                settingsService,
                iconsPath,
                options.Value.LogViewerPort,
                menuHandler,
                sttServiceManager,
                mistralProvider,
                dictationStateMachine,
                dictationWorker);
        });

        return services;
    }
}
