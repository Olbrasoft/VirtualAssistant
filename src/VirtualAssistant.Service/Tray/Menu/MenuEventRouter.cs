using Microsoft.Extensions.Logging;

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

    public MenuEventRouter(ILogger<MenuEventRouter> logger, IMenuStateManager stateManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
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
    /// Event fired when user selects Show Logs.
    /// </summary>
    public event Action? OnShowLogsRequested;

    /// <summary>
    /// Event fired when user wants to stop log-viewer service.
    /// </summary>
    public event Action? OnStopLogViewerRequested;

    /// <summary>
    /// Event fired when user wants to start log-viewer service.
    /// </summary>
    public event Action? OnStartLogViewerRequested;

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
    public event Action? OnTtsMuteToggleRequested;

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

        switch (id)
        {
            case MenuItemIds.QuitId:
                _logger.LogInformation("Quit menu item clicked");
                OnQuitRequested?.Invoke();
                break;

            case MenuItemIds.MuteToggleId:
                _logger.LogInformation("Mute toggle menu item clicked");
                OnMuteToggleRequested?.Invoke();
                break;

            case MenuItemIds.TtsMuteToggleId:
                _logger.LogInformation("TTS mute toggle clicked");
                OnTtsMuteToggleRequested?.Invoke();
                break;

            case MenuItemIds.ShowLogsId:
                _logger.LogInformation("Show logs menu item clicked");
                OnShowLogsRequested?.Invoke();
                break;

            case MenuItemIds.LogViewerId:
                _logger.LogInformation("Log Viewer service menu item clicked");
                if (_stateManager.LogViewerStatus == "Running")
                {
                    OnStopLogViewerRequested?.Invoke();
                }
                else
                {
                    OnStartLogViewerRequested?.Invoke();
                }
                break;

            case MenuItemIds.LlmCorrectionId:
                _logger.LogInformation("LLM Correction menu item clicked (current: {Enabled})", _stateManager.IsLlmCorrectionEnabled);
                // Toggle LLM correction
                var newLlmState = !_stateManager.IsLlmCorrectionEnabled;
                _stateManager.UpdateLlmCorrectionStatus(newLlmState);
                OnLlmCorrectionToggled?.Invoke(newLlmState);
                break;

            case MenuItemIds.ReloadPromptId:
                _logger.LogInformation("Reload LLM Prompt menu item clicked");
                OnReloadPromptRequested?.Invoke();
                break;

            case MenuItemIds.DictationToggleId:
                _logger.LogInformation("Dictation toggle clicked (current: {Enabled})", _stateManager.IsDictationEnabled);
                // Toggle dictation
                var newDictationState = !_stateManager.IsDictationEnabled;
                _stateManager.UpdateDictationStatus(newDictationState);
                OnDictationToggleRequested?.Invoke(newDictationState);
                break;

            default:
                _logger.LogWarning("Unknown menu item clicked: id={Id}", id);
                break;
        }
    }
}
