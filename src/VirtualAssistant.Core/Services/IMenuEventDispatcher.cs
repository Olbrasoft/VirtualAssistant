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
    /// Sets text-to-speech muting state.
    /// </summary>
    /// <param name="muted">True to mute TTS, false to unmute.</param>
    Task HandleTtsMuteToggleAsync(bool muted);

    /// <summary>
    /// Handles dashboard request from menu.
    /// Opens browser to admin dashboard.
    /// </summary>
    void HandleDashboard();

    /// <summary>
    /// Handles about dialog request from menu.
    /// Shows application version and information.
    /// </summary>
    void HandleAbout();

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

    /// <summary>
    /// Handles Mercury Billing request from menu.
    /// Opens browser to Inception Labs billing dashboard.
    /// </summary>
    void HandleMercuryBilling();

    /// <summary>
    /// Handles streaming transcription toggle from menu (fast dictation path only).
    /// </summary>
    void HandleStreamingTranscriptionToggle(bool enabled);
}
