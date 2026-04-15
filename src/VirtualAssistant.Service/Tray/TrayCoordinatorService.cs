using Olbrasoft.VirtualAssistant.Core.Services;
using SystemTrayMenuHandler = Olbrasoft.SystemTray.Linux.ITrayMenuHandler;

namespace Olbrasoft.VirtualAssistant.Service.Tray;

/// <summary>
/// Orchestrates tray icon functionality by delegating to specialized services.
/// Replaces GOD class VirtualAssistantTrayService (692 lines) with composition of 5 focused services.
/// Implements Dependency Inversion Principle and Single Responsibility Principle.
/// </summary>
public class TrayCoordinatorService : IDisposable
{
    private readonly ILogger<TrayCoordinatorService> _logger;
    private readonly ITrayIconCoordinator _iconCoordinator;
    private readonly IMenuEventDispatcher _menuDispatcher;
    private readonly IServiceLifecycleManager _lifecycleManager;
    private readonly IStateNotificationHandler _stateHandler;
    private readonly IIconAnimationService _iconAnimationService;
    private readonly SystemTrayMenuHandler? _menuHandler;
    private bool _disposed;

    /// <summary>
    /// Event fired when user requests to quit the application.
    /// </summary>
    public event Action? OnQuitRequested;

    public TrayCoordinatorService(
        ILogger<TrayCoordinatorService> logger,
        ITrayIconCoordinator iconCoordinator,
        IMenuEventDispatcher menuDispatcher,
        IServiceLifecycleManager lifecycleManager,
        IStateNotificationHandler stateHandler,
        IIconAnimationService iconAnimationService,
        SystemTrayMenuHandler? menuHandler = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _iconCoordinator = iconCoordinator ?? throw new ArgumentNullException(nameof(iconCoordinator));
        _menuDispatcher = menuDispatcher ?? throw new ArgumentNullException(nameof(menuDispatcher));
        _lifecycleManager = lifecycleManager ?? throw new ArgumentNullException(nameof(lifecycleManager));
        _stateHandler = stateHandler ?? throw new ArgumentNullException(nameof(stateHandler));
        _iconAnimationService = iconAnimationService ?? throw new ArgumentNullException(nameof(iconAnimationService));
        _menuHandler = menuHandler;

        WireMenuHandlerEvents();
    }

    /// <summary>
    /// Initializes all tray services and icons.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing tray coordinator service");

            // Initialize icons (3 tray icons: left hand, center, right hand)
            await _iconCoordinator.InitializeIconsAsync();

            // Subscribe to state change events and initialize states
            _stateHandler.SubscribeToEvents();
            await _stateHandler.InitializeStatesAsync();

            _logger.LogInformation("Tray coordinator service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize tray coordinator service");
            throw;
        }
    }

    /// <summary>
    /// Wires menu handler events to appropriate service methods.
    /// </summary>
    private void WireMenuHandlerEvents()
    {
        if (_menuHandler is VirtualAssistantDBusMenuHandler handler)
        {
            // Quit event
            handler.OnQuitRequested += () => OnQuitRequested?.Invoke();

            // Menu events -> MenuEventDispatcher
            handler.OnMuteToggleRequested += _menuDispatcher.HandleMuteToggle;
            handler.OnTtsMuteToggleRequested += async (muted) => await _menuDispatcher.HandleTtsMuteToggleAsync(muted);
            handler.OnDashboardRequested += _menuDispatcher.HandleDashboard;
            handler.OnAboutRequested += _menuDispatcher.HandleAbout;
            handler.OnLlmCorrectionToggled += _menuDispatcher.HandleLlmCorrectionToggle;
            handler.OnReloadPromptRequested += _menuDispatcher.HandleReloadPrompt;
            handler.OnDictationToggleRequested += _menuDispatcher.HandleDictationToggle;
            handler.OnMercuryBillingRequested += _menuDispatcher.HandleMercuryBilling;
            handler.OnStreamingTranscriptionToggled += _menuDispatcher.HandleStreamingTranscriptionToggle;

            _logger.LogDebug("Menu handler events wired up successfully");
        }
        else
        {
            _logger.LogWarning("Menu handler is not VirtualAssistantDBusMenuHandler type: {Type}",
                _menuHandler?.GetType().FullName ?? "null");
        }
    }

    /// <summary>
    /// Disposes resources and unsubscribes from events.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            // Unsubscribe from state change events
            _stateHandler.UnsubscribeFromEvents();

            // Note: Cannot unwire async lambda events - they are different instances
            // Event cleanup happens when handler is disposed by framework
            _logger.LogDebug("Tray coordinator service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing tray coordinator service");
        }

        GC.SuppressFinalize(this);
    }
}
