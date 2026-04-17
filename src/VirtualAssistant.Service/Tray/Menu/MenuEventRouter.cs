namespace Olbrasoft.VirtualAssistant.Service.Tray.Menu;

/// <summary>
/// Routes menu click events to appropriate handlers.
/// Publishes events that external components can subscribe to.
/// Manages toggle state for LLM correction and dictation.
/// </summary>
public class MenuEventRouter : IMenuEventRouter
{
    private readonly ILogger<MenuEventRouter> _logger;
    private readonly IMenuStateManager _stateManager;
    private readonly Dictionary<int, Action> _menuHandlers;

    public MenuEventRouter(ILogger<MenuEventRouter> logger, IMenuStateManager stateManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _menuHandlers = CreateMenuItemToHandlerMap();
    }

    /// <summary>
    /// Event fired when user selects Quit from the menu.
    /// </summary>
    public event Action? OnQuitRequested;

    /// <summary>
    /// Event fired when user selects Mute/Unmute toggle.
    /// </summary>
    public event Action? OnMuteToggleRequested;

    /// <summary>
    /// Event fired when user selects Dashboard menu item.
    /// </summary>
    public event Action? OnDashboardRequested;

    /// <summary>
    /// Event fired when user selects About menu item.
    /// </summary>
    public event Action? OnAboutRequested;

    /// <summary>
    /// Event fired when user selects Mercury Billing menu item.
    /// </summary>
    public event Action? OnMercuryBillingRequested;

    /// <summary>
    /// Event fired when user toggles LLM correction.
    /// </summary>
    public event Action<bool>? OnLlmCorrectionToggled;

    /// <summary>
    /// Event fired when user wants to reload the Mistral prompt.
    /// </summary>
    public event Action? OnReloadPromptRequested;

    /// <summary>
    /// Event fired when user toggles dictation on/off.
    /// </summary>
    public event Action<bool>? OnDictationToggleRequested;

    /// <summary>
    /// Event fired when user toggles TTS mute on/off.
    /// </summary>
    public event Action<bool>? OnTtsMuteToggleRequested;

    /// <summary>
    /// Event fired when user toggles streaming transcription on/off.
    /// </summary>
    public event Action<bool>? OnStreamingTranscriptionToggled;

    /// <summary>
    /// Event fired when user clicks "Obnovit cache" for transcription corrections.
    /// </summary>
    public event Action? OnReloadCorrectionsCacheRequested;

    /// <summary>
    /// Handles a menu click event.
    /// Routes the event to the appropriate handler based on menu item ID.
    /// </summary>
    public void HandleMenuEvent(int id, string eventId)
    {
        _logger.LogInformation("Event received: id={Id}, eventId={EventId}", id, eventId);

        if (eventId != "clicked")
        {
            return;
        }

        if (_menuHandlers.TryGetValue(id, out var handler))
        {
            handler();
        }
        else
        {
            _logger.LogWarning("Unknown menu item clicked: id={Id}", id);
        }
    }

    private Dictionary<int, Action> CreateMenuItemToHandlerMap() => new()
    {
        [MenuItemIds.QuitId] = HandleQuit,
        [MenuItemIds.MuteToggleId] = HandleMuteToggle,
        [MenuItemIds.TtsMuteToggleId] = HandleTtsMuteToggle,
        [MenuItemIds.DashboardId] = HandleDashboard,
        [MenuItemIds.AboutId] = HandleAbout,
        [MenuItemIds.MercuryBillingId] = HandleMercuryBilling,
        [MenuItemIds.LlmCorrectionId] = HandleLlmCorrection,
        [MenuItemIds.ReloadPromptId] = HandleReloadPrompt,
        [MenuItemIds.DictationToggleId] = HandleDictationToggle,
        [MenuItemIds.StreamingTranscriptionId] = HandleStreamingTranscriptionToggle,
        [MenuItemIds.ReloadCorrectionsCacheId] = HandleReloadCorrectionsCache
    };

    private void HandleQuit()
    {
        _logger.LogInformation("Quit menu item clicked");
        OnQuitRequested?.Invoke();
    }

    private void HandleMuteToggle()
    {
        _logger.LogInformation("Mute toggle menu item clicked");
        OnMuteToggleRequested?.Invoke();
    }

    private void HandleTtsMuteToggle()
    {
        _logger.LogInformation("TTS mute toggle clicked (current: {Muted})", _stateManager.IsTtsMuted);
        var newState = !_stateManager.IsTtsMuted;
        _stateManager.UpdateTtsMuteState(newState);
        OnTtsMuteToggleRequested?.Invoke(newState);
    }

    private void HandleDashboard()
    {
        _logger.LogInformation("Dashboard menu item clicked");
        OnDashboardRequested?.Invoke();
    }

    private void HandleAbout()
    {
        _logger.LogInformation("About menu item clicked");
        OnAboutRequested?.Invoke();
    }

    private void HandleMercuryBilling()
    {
        _logger.LogInformation("Mercury Billing menu item clicked");
        OnMercuryBillingRequested?.Invoke();
    }

    private void HandleLlmCorrection()
    {
        _logger.LogInformation("LLM Correction menu item clicked (current: {Enabled})", _stateManager.IsLlmCorrectionEnabled);
        var newState = !_stateManager.IsLlmCorrectionEnabled;
        _stateManager.UpdateLlmCorrectionStatus(newState);
        OnLlmCorrectionToggled?.Invoke(newState);
    }

    private void HandleReloadPrompt()
    {
        _logger.LogInformation("Reload LLM Prompt menu item clicked");
        OnReloadPromptRequested?.Invoke();
    }

    private void HandleDictationToggle()
    {
        _logger.LogInformation("Dictation toggle clicked (current: {Enabled})", _stateManager.IsDictationEnabled);
        var newState = !_stateManager.IsDictationEnabled;
        _stateManager.UpdateDictationStatus(newState);
        OnDictationToggleRequested?.Invoke(newState);
    }

    private void HandleStreamingTranscriptionToggle()
    {
        _logger.LogInformation("Streaming transcription toggle clicked (current: {Enabled})", _stateManager.IsStreamingTranscriptionEnabled);
        var newState = !_stateManager.IsStreamingTranscriptionEnabled;
        _stateManager.UpdateStreamingTranscriptionStatus(newState);
        OnStreamingTranscriptionToggled?.Invoke(newState);
    }

    private void HandleReloadCorrectionsCache()
    {
        _logger.LogInformation("Reload corrections cache menu item clicked");
        OnReloadCorrectionsCacheRequested?.Invoke();
    }
}
