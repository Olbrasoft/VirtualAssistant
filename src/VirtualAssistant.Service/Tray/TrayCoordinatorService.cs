using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Tray.Menu;

namespace Olbrasoft.VirtualAssistant.Service.Tray;

/// <summary>
/// Orchestrates tray icon functionality by delegating to specialized services.
/// Replaces GOD class VirtualAssistantTrayService (692 lines) with composition of focused services.
/// Implements Dependency Inversion Principle and Single Responsibility Principle.
/// </summary>
public class TrayCoordinatorService : IDisposable
{
    private readonly ILogger<TrayCoordinatorService> _logger;
    private readonly ITrayIconCoordinator _iconCoordinator;
    private readonly IMenuEventDispatcher _menuDispatcher;
    private readonly IMenuEventForwarder _menuEventForwarder;
    private readonly IServiceLifecycleManager _lifecycleManager;
    private readonly IStateNotificationHandler _stateHandler;
    private readonly IIconAnimationService _iconAnimationService;

    // Stored handlers for clean unsubscription in Dispose. Inline lambdas
    // would give each += a fresh delegate instance and -= would not remove it.
    private readonly Action _quitHandler;
    private readonly Action _muteToggleHandler;
    private readonly Action<bool> _ttsMuteToggleHandler;
    private readonly Action _dashboardHandler;
    private readonly Action _aboutHandler;
    private readonly Action<bool> _llmCorrectionHandler;
    private readonly Action _reloadPromptHandler;
    private readonly Action<bool> _dictationToggleHandler;
    private readonly Action _mercuryBillingHandler;
    private readonly Action<bool> _streamingTranscriptionHandler;
    private readonly Action _reloadCorrectionsCacheHandler;

    private bool _disposed;

    /// <summary>
    /// Event fired when the user requests to quit the application.
    /// </summary>
    public event Action? OnQuitRequested;

    public TrayCoordinatorService(
        ILogger<TrayCoordinatorService> logger,
        ITrayIconCoordinator iconCoordinator,
        IMenuEventDispatcher menuDispatcher,
        IMenuEventForwarder menuEventForwarder,
        IServiceLifecycleManager lifecycleManager,
        IStateNotificationHandler stateHandler,
        IIconAnimationService iconAnimationService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _iconCoordinator = iconCoordinator ?? throw new ArgumentNullException(nameof(iconCoordinator));
        _menuDispatcher = menuDispatcher ?? throw new ArgumentNullException(nameof(menuDispatcher));
        _menuEventForwarder = menuEventForwarder ?? throw new ArgumentNullException(nameof(menuEventForwarder));
        _lifecycleManager = lifecycleManager ?? throw new ArgumentNullException(nameof(lifecycleManager));
        _stateHandler = stateHandler ?? throw new ArgumentNullException(nameof(stateHandler));
        _iconAnimationService = iconAnimationService ?? throw new ArgumentNullException(nameof(iconAnimationService));

        _quitHandler = () => OnQuitRequested?.Invoke();
        _muteToggleHandler = () => _menuDispatcher.HandleMuteToggle();
        _ttsMuteToggleHandler = muted => _ = _menuDispatcher.HandleTtsMuteToggleAsync(muted);
        _dashboardHandler = () => _menuDispatcher.HandleDashboard();
        _aboutHandler = () => _menuDispatcher.HandleAbout();
        _llmCorrectionHandler = enabled => _menuDispatcher.HandleLlmCorrectionToggle(enabled);
        _reloadPromptHandler = () => _menuDispatcher.HandleReloadPrompt();
        _dictationToggleHandler = enabled => _menuDispatcher.HandleDictationToggle(enabled);
        _mercuryBillingHandler = () => _menuDispatcher.HandleMercuryBilling();
        _streamingTranscriptionHandler = enabled => _menuDispatcher.HandleStreamingTranscriptionToggle(enabled);
        _reloadCorrectionsCacheHandler = () => _menuDispatcher.HandleReloadCorrectionsCache();

        WireMenuEvents();
    }

    /// <summary>
    /// Initializes all tray services and icons.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing tray coordinator service");

            await _iconCoordinator.InitializeIconsAsync();

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

    private void WireMenuEvents()
    {
        _menuEventForwarder.OnQuitRequested += _quitHandler;
        _menuEventForwarder.OnMuteToggleRequested += _muteToggleHandler;
        _menuEventForwarder.OnTtsMuteToggleRequested += _ttsMuteToggleHandler;
        _menuEventForwarder.OnDashboardRequested += _dashboardHandler;
        _menuEventForwarder.OnAboutRequested += _aboutHandler;
        _menuEventForwarder.OnLlmCorrectionToggled += _llmCorrectionHandler;
        _menuEventForwarder.OnReloadPromptRequested += _reloadPromptHandler;
        _menuEventForwarder.OnDictationToggleRequested += _dictationToggleHandler;
        _menuEventForwarder.OnMercuryBillingRequested += _mercuryBillingHandler;
        _menuEventForwarder.OnStreamingTranscriptionToggled += _streamingTranscriptionHandler;
        _menuEventForwarder.OnReloadCorrectionsCacheRequested += _reloadCorrectionsCacheHandler;
        _logger.LogDebug("Menu event forwarder wired up");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _menuEventForwarder.OnQuitRequested -= _quitHandler;
            _menuEventForwarder.OnMuteToggleRequested -= _muteToggleHandler;
            _menuEventForwarder.OnTtsMuteToggleRequested -= _ttsMuteToggleHandler;
            _menuEventForwarder.OnDashboardRequested -= _dashboardHandler;
            _menuEventForwarder.OnAboutRequested -= _aboutHandler;
            _menuEventForwarder.OnLlmCorrectionToggled -= _llmCorrectionHandler;
            _menuEventForwarder.OnReloadPromptRequested -= _reloadPromptHandler;
            _menuEventForwarder.OnDictationToggleRequested -= _dictationToggleHandler;
            _menuEventForwarder.OnMercuryBillingRequested -= _mercuryBillingHandler;
            _menuEventForwarder.OnStreamingTranscriptionToggled -= _streamingTranscriptionHandler;
            _menuEventForwarder.OnReloadCorrectionsCacheRequested -= _reloadCorrectionsCacheHandler;

            _stateHandler.UnsubscribeFromEvents();
            _logger.LogDebug("Tray coordinator service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing tray coordinator service");
        }

        GC.SuppressFinalize(this);
    }
}
