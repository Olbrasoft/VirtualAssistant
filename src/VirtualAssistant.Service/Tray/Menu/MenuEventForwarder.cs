namespace Olbrasoft.VirtualAssistant.Service.Tray.Menu;

/// <inheritdoc />
public sealed class MenuEventForwarder : IMenuEventForwarder
{
    private readonly IMenuEventRouter _router;

    // Stored delegate references so we can unsubscribe in Dispose. An inline
    // lambda in +=  creates a fresh delegate each call, which -= would not
    // remove, causing leaks over the tray lifetime.
    private readonly Action _quitHandler;
    private readonly Action _muteToggleHandler;
    private readonly Action _dashboardHandler;
    private readonly Action _aboutHandler;
    private readonly Action<bool> _llmCorrectionHandler;
    private readonly Action _reloadPromptHandler;
    private readonly Action<bool> _dictationToggleHandler;
    private readonly Action<bool> _ttsMuteToggleHandler;
    private readonly Action _mercuryBillingHandler;
    private readonly Action<bool> _streamingTranscriptionHandler;
    private readonly Action _reloadCorrectionsCacheHandler;

    public event Action? OnQuitRequested;
    public event Action? OnMuteToggleRequested;
    public event Action? OnDashboardRequested;
    public event Action? OnAboutRequested;
    public event Action<bool>? OnLlmCorrectionToggled;
    public event Action? OnReloadPromptRequested;
    public event Action<bool>? OnDictationToggleRequested;
    public event Action<bool>? OnTtsMuteToggleRequested;
    public event Action? OnMercuryBillingRequested;
    public event Action<bool>? OnStreamingTranscriptionToggled;
    public event Action? OnReloadCorrectionsCacheRequested;

    public MenuEventForwarder(IMenuEventRouter router)
    {
        _router = router ?? throw new ArgumentNullException(nameof(router));

        _quitHandler = () => OnQuitRequested?.Invoke();
        _muteToggleHandler = () => OnMuteToggleRequested?.Invoke();
        _dashboardHandler = () => OnDashboardRequested?.Invoke();
        _aboutHandler = () => OnAboutRequested?.Invoke();
        _llmCorrectionHandler = enabled => OnLlmCorrectionToggled?.Invoke(enabled);
        _reloadPromptHandler = () => OnReloadPromptRequested?.Invoke();
        _dictationToggleHandler = enabled => OnDictationToggleRequested?.Invoke(enabled);
        _ttsMuteToggleHandler = muted => OnTtsMuteToggleRequested?.Invoke(muted);
        _mercuryBillingHandler = () => OnMercuryBillingRequested?.Invoke();
        _streamingTranscriptionHandler = enabled => OnStreamingTranscriptionToggled?.Invoke(enabled);
        _reloadCorrectionsCacheHandler = () => OnReloadCorrectionsCacheRequested?.Invoke();

        _router.OnQuitRequested += _quitHandler;
        _router.OnMuteToggleRequested += _muteToggleHandler;
        _router.OnDashboardRequested += _dashboardHandler;
        _router.OnAboutRequested += _aboutHandler;
        _router.OnLlmCorrectionToggled += _llmCorrectionHandler;
        _router.OnReloadPromptRequested += _reloadPromptHandler;
        _router.OnDictationToggleRequested += _dictationToggleHandler;
        _router.OnTtsMuteToggleRequested += _ttsMuteToggleHandler;
        _router.OnMercuryBillingRequested += _mercuryBillingHandler;
        _router.OnStreamingTranscriptionToggled += _streamingTranscriptionHandler;
        _router.OnReloadCorrectionsCacheRequested += _reloadCorrectionsCacheHandler;
    }

    public void Dispose()
    {
        _router.OnQuitRequested -= _quitHandler;
        _router.OnMuteToggleRequested -= _muteToggleHandler;
        _router.OnDashboardRequested -= _dashboardHandler;
        _router.OnAboutRequested -= _aboutHandler;
        _router.OnLlmCorrectionToggled -= _llmCorrectionHandler;
        _router.OnReloadPromptRequested -= _reloadPromptHandler;
        _router.OnDictationToggleRequested -= _dictationToggleHandler;
        _router.OnTtsMuteToggleRequested -= _ttsMuteToggleHandler;
        _router.OnMercuryBillingRequested -= _mercuryBillingHandler;
        _router.OnStreamingTranscriptionToggled -= _streamingTranscriptionHandler;
        _router.OnReloadCorrectionsCacheRequested -= _reloadCorrectionsCacheHandler;
    }
}
