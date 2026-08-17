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
        // Menu state manager (also implements IServiceStatusUpdater after the
        // #980 DBusMenuHandler split — callers like StateNotificationHandler
        // and ServiceLifecycleManager now depend on it directly rather than
        // on the D-Bus handler cast).
        services.AddSingleton<MenuStateManager>();
        services.AddSingleton<IMenuStateManager>(sp => sp.GetRequiredService<MenuStateManager>());
        services.AddSingleton<IServiceStatusUpdater>(sp => sp.GetRequiredService<MenuStateManager>());

        // Menu layout builder (issue #468 - SRP refactoring)
        services.AddSingleton<IMenuLayoutBuilder, MenuLayoutBuilder>();

        // Menu event router (issue #468 - SRP refactoring)
        services.AddSingleton<IMenuEventRouter, MenuEventRouter>();

        // Menu event forwarder (issue #980 - owns the 11 menu-click events
        // that TrayCoordinatorService subscribes handlers to, plus the stored
        // delegates needed to unsubscribe cleanly on shutdown).
        services.AddSingleton<IMenuEventForwarder, MenuEventForwarder>();

        // D-Bus menu handler for tray icon context menu — after the #980
        // split this class only speaks the com.canonical.dbusmenu protocol
        // and emits layout-changed signals when MenuStateManager fires
        // StateChanged. All application-level state updates and event
        // forwarding moved to the two classes above.
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

        // Service lifecycle manager for managing dependent services.
        // After the #980 split IServiceStatusUpdater is implemented directly by
        // MenuStateManager; no longer cast from the D-Bus handler.
        services.AddSingleton<IServiceLifecycleManager>(sp =>
            new ServiceLifecycleManager(
                sp.GetRequiredService<ILogger<ServiceLifecycleManager>>(),
                sp.GetRequiredService<IOptions<ServiceMonitoringOptions>>(),
                sp.GetRequiredService<ISystemdServiceController>(),
                sp.GetRequiredService<IServiceStatusUpdater>()));

        // Icon animation service for hand icon animations
        services.AddSingleton<IIconAnimationService, IconAnimationService>();

        // Menu event dispatcher for handling tray menu actions. The four
        // domain handlers are constructed inline so the DI graph reads top-
        // down (which handler owns which optional service) instead of being
        // scattered across separate AddSingleton calls.
        services.AddSingleton<IMenuEventDispatcher>(sp =>
        {
            // Read Dashboard:BaseUrl as-is and let DashboardMenuHandler apply its
            // own fallback. Keeping the default in one place (the handler) avoids
            // the drift that Copilot called out on #1006 where the DI layer
            // coalesced first and the handler's nullable-arg docs lied about it.
            var dashboardBaseUrl = sp.GetRequiredService<IConfiguration>()["Dashboard:BaseUrl"];

            var mute = new Tray.Handlers.MuteMenuHandler(
                sp.GetRequiredService<ILogger<Tray.Handlers.MuteMenuHandler>>(),
                sp.GetRequiredService<IManualMuteService>(),
                sp.GetRequiredService<ISettingsService>());

            var dictation = new Tray.Handlers.DictationMenuHandler(
                sp.GetRequiredService<ILogger<Tray.Handlers.DictationMenuHandler>>(),
                sp.GetService<IDictationControl>());

            var llm = new Tray.Handlers.LlmMenuHandler(
                sp.GetRequiredService<ILogger<Tray.Handlers.LlmMenuHandler>>(),
                sp.GetService<ILlmProvider>(),
                sp.GetService<IPromptSyncService>(),
                sp.GetService<IMenuStateManager>(),
                sp.GetService<Olbrasoft.VirtualAssistant.Voice.Filters.DatabaseCorrectionFilterStrategy>(),
                sp.GetServices<ILlmProvider>());

            var dashboard = new Tray.Handlers.DashboardMenuHandler(
                sp.GetRequiredService<ILogger<Tray.Handlers.DashboardMenuHandler>>(),
                dashboardBaseUrl);

            return new MenuEventDispatcher(mute, dictation, llm, dashboard);
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
            // Force-resolve the D-Bus handler so the singleton is constructed
            // before StateNotificationHandler starts publishing updates — the
            // actual D-Bus registration happens later in
            // VirtualAssistantDBusMenuHandler.RegisterWithDbus(Connection).
            // The previous `menuHandler as IServiceStatusUpdater` cast went
            // away with #1007; this discard keeps the construction-order
            // dependency explicit.
            _ = sp.GetRequiredService<SystemTrayMenuHandler>();
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
                sp.GetRequiredService<IServiceStatusUpdater>(),
                iconCoordinator,
                iconAnimationService,
                lifecycleManager,
                dictationStateMachine,
                dictationWorker,
                recordingNotificationService,
                recordingOverlayService,
                recordingStartSoundPlayer);
        });

        // Tray coordinator service (orchestrates tray subsystems).
        // Note: requires SystemTrayMenuHandler to be resolved first so the
        // D-Bus handler is live before menu clicks can be forwarded.
        services.AddSingleton(sp =>
        {
            // Resolve the D-Bus handler up-front so it's registered with D-Bus
            // by the time the forwarder starts handing us events.
            _ = sp.GetRequiredService<SystemTrayMenuHandler>();

            return new TrayCoordinatorService(
                sp.GetRequiredService<ILogger<TrayCoordinatorService>>(),
                sp.GetRequiredService<ITrayIconCoordinator>(),
                sp.GetRequiredService<IMenuEventDispatcher>(),
                sp.GetRequiredService<IMenuEventForwarder>(),
                sp.GetRequiredService<IStateNotificationHandler>());
        });

        return services;
    }
}
