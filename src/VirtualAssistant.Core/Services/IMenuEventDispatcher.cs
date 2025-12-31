namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Handles menu events dispatched from tray icon context menu.
/// Implements Command pattern for menu actions.
/// </summary>
public interface IMenuEventDispatcher
{
    /// <summary>
    /// Handles mute toggle request from menu.
    /// Toggles microphone mute state.
    /// </summary>
    void HandleMuteToggle();

    /// <summary>
    /// Handles TTS mute toggle request from menu.
    /// Toggles text-to-speech muting state.
    /// </summary>
    Task HandleTtsMuteToggleAsync();

    /// <summary>
    /// Handles show logs request from menu.
    /// Opens browser to logs viewer.
    /// </summary>
    void HandleShowLogs();

    /// <summary>
    /// Handles LLM correction toggle request from menu.
    /// Enables or disables LLM-based transcription correction.
    /// </summary>
    /// <param name="enabled">True to enable LLM correction, false to disable.</param>
    void HandleLlmCorrectionToggle(bool enabled);

    /// <summary>
    /// Handles reload LLM prompt request from menu.
    /// Copies prompt from source to deployment location and clears cache.
    /// </summary>
    void HandleReloadPrompt();

    /// <summary>
    /// Handles dictation enable/disable toggle from menu.
    /// </summary>
    /// <param name="enabled">True to enable dictation, false to disable.</param>
    void HandleDictationToggle(bool enabled);
}
