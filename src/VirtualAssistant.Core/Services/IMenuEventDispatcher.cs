namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Dispatches menu events to appropriate handlers using Command pattern.
/// Implements Single Responsibility Principle - only handles menu event routing.
/// </summary>
public interface IMenuEventDispatcher
{
    /// <summary>
    /// Handles mute toggle request from menu.
    /// </summary>
    void HandleMuteToggle();

    /// <summary>
    /// Handles TTS mute toggle request from menu.
    /// </summary>
    Task HandleTtsMuteToggleAsync();

    /// <summary>
    /// Handles show logs request from menu.
    /// </summary>
    void HandleShowLogs();

    /// <summary>
    /// Handles LLM correction toggle request from menu.
    /// </summary>
    /// <param name="enabled">Whether LLM correction should be enabled</param>
    void HandleLlmCorrectionToggle(bool enabled);

    /// <summary>
    /// Handles reload LLM prompt request from menu.
    /// </summary>
    void HandleReloadPrompt();

    /// <summary>
    /// Handles dictation toggle request from menu.
    /// </summary>
    /// <param name="enabled">Whether dictation should be enabled</param>
    void HandleDictationToggle(bool enabled);
}
