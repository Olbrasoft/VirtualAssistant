using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Desktop.Services;
using Olbrasoft.VirtualAssistant.Desktop.UI;
using Olbrasoft.VirtualAssistant.Service.Configuration;
using Olbrasoft.VirtualAssistant.Service.Infrastructure;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Service.Tray;
using Olbrasoft.VirtualAssistant.Service.Tray.Menu;
using Olbrasoft.VirtualAssistant.Service.Workers;
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

        // Prompt sync configuration
        services.Configure<PromptSyncOptions>(
            configuration.GetSection(PromptSyncOptions.SectionName));

        // Prompt sync service for copying prompts from source to deployment
        services.AddSingleton<IPromptSyncService, PromptSyncService>();

        // Background worker for checking prompt sync status
        services.AddHostedService<PromptSyncCheckWorker>();

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
            var iconsPath = Path.Combine(AppContext.BaseDirectory, "assets", "icons");
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

        // Systemd service controller for managing systemd services (OCP - issue #650)
        services.AddSingleton<ISystemdServiceController, SystemdServiceController>();

        // Service lifecycle manager for managing dependent services
        services.AddSingleton<IServiceLifecycleManager>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ServiceLifecycleManager>>();
            var options = sp.GetRequiredService<IOptions<ServiceMonitoringOptions>>();
            var serviceController = sp.GetRequiredService<ISystemdServiceController>();
            var menuHandler = sp.GetRequiredService<SystemTrayMenuHandler>();
            return new ServiceLifecycleManager(logger, options, serviceController, menuHandler as IServiceStatusUpdater);
        });

        // Icon animation service for hand icon animations
        services.AddSingleton<IIconAnimationService, IconAnimationService>();

        // Menu event dispatcher for handling tray menu actions
        services.AddSingleton<IMenuEventDispatcher>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<MenuEventDispatcher>>();
            var muteService = sp.GetRequiredService<IManualMuteService>();
            var settingsService = sp.GetRequiredService<ISettingsService>();
            var configuration = sp.GetRequiredService<IConfiguration>();
            var llmProvider = sp.GetService<ILlmProvider>();
            var dictationControl = sp.GetService<IDictationControl>();
            var promptSyncService = sp.GetService<IPromptSyncService>();
            var menuStateManager = sp.GetService<IMenuStateManager>();

            var dashboardBaseUrl = configuration["Dashboard:BaseUrl"] ?? "http://localhost:5055";

            return new MenuEventDispatcher(
                logger,
                muteService,
                settingsService,
                dashboardBaseUrl,
                llmProvider,
                dictationControl,
                promptSyncService,
                menuStateManager);
        });

        // Recording notification service for dictation status (Phase 1 - issue #670)
        services.AddSingleton<IRecordingNotificationService, RecordingNotificationService>();

        // Cursor position service for overlay positioning (Phase 2 - issue #671)
        services.AddSingleton<ICursorPositionService, CursorPositionService>();

        // Recording overlay window (GTK4 LayerShell implementation)
        services.AddSingleton<IRecordingOverlayWindow>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RecordingOverlayWindow>>();
            return new RecordingOverlayWindow(logger);
        });

        // Recording overlay service (Phase 2 - issue #672, #677)
        // Uses GTK4 LayerShell for overlay window near cursor
        services.AddSingleton<IRecordingOverlayService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RecordingOverlayService>>();
            var cursorPositionService = sp.GetRequiredService<ICursorPositionService>();
            var overlayWindow = sp.GetRequiredService<IRecordingOverlayWindow>();
            return new RecordingOverlayService(logger, cursorPositionService, overlayWindow);
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
            var dictationStateMachine = sp.GetService<Voice.StateMachine.IDictationStateMachine>();
            var dictationWorker = sp.GetService<DictationWorker>();
            var recordingNotificationService = sp.GetService<IRecordingNotificationService>();
            var recordingOverlayService = sp.GetService<IRecordingOverlayService>();
            var recordingStartSoundPlayer = sp.GetKeyedService<ISoundEffectPlayer>("recording-start");

            return new StateNotificationHandler(
                logger,
                muteService,
                settingsService,
                (IServiceStatusUpdater)menuHandler,
                iconCoordinator,
                iconAnimationService,
                lifecycleManager,
                dictationStateMachine,
                dictationWorker,
                recordingNotificationService,
                recordingOverlayService,
                recordingStartSoundPlayer);
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
