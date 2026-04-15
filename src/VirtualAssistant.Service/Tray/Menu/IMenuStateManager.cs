namespace Olbrasoft.VirtualAssistant.Service.Tray.Menu;

/// <summary>
/// Manages the state of all menu items.
/// Tracks mute status, service statuses, and feature toggles.
/// </summary>
public interface IMenuStateManager
{
    /// <summary>
    /// Event raised when menu state changes and layout needs to be refreshed.
    /// </summary>
    event EventHandler<MenuStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Gets the current revision number for D-Bus menu updates.
    /// </summary>
    uint Revision { get; }

    /// <summary>
    /// Gets whether the assistant is muted.
    /// </summary>
    bool IsMuted { get; }

    /// <summary>
    /// Gets whether TTS is muted.
    /// </summary>
    bool IsTtsMuted { get; }

    /// <summary>
    /// Gets the current status of the log viewer service.
    /// </summary>
    string LogViewerStatus { get; }

    /// <summary>
    /// Gets whether LLM correction is enabled.
    /// </summary>
    bool IsLlmCorrectionEnabled { get; }

    /// <summary>
    /// Gets whether dictation is enabled.
    /// </summary>
    bool IsDictationEnabled { get; }

    /// <summary>
    /// Gets whether streaming (chunked) transcription is enabled for the fast dictation path.
    /// </summary>
    bool IsStreamingTranscriptionEnabled { get; }

    /// <summary>
    /// Gets the current prompt synchronization status.
    /// </summary>
    PromptSyncStatus PromptSyncStatus { get; }

    /// <summary>
    /// Gets the error message from the last failed sync, if any.
    /// </summary>
    string? LastSyncError { get; }

    /// <summary>
    /// Updates the mute state.
    /// </summary>
    void UpdateMuteState(bool isMuted);

    /// <summary>
    /// Updates the TTS mute state.
    /// </summary>
    void UpdateTtsMuteState(bool isMuted);

    /// <summary>
    /// Updates the log viewer service status.
    /// </summary>
    void UpdateLogViewerStatus(bool isRunning);

    /// <summary>
    /// Updates the LLM correction enabled status.
    /// </summary>
    void UpdateLlmCorrectionStatus(bool enabled);

    /// <summary>
    /// Updates the dictation enabled status.
    /// </summary>
    void UpdateDictationStatus(bool enabled);

    /// <summary>
    /// Updates the streaming transcription enabled status (fast path only).
    /// </summary>
    void UpdateStreamingTranscriptionStatus(bool enabled);

    /// <summary>
    /// Updates the prompt synchronization status.
    /// </summary>
    /// <param name="status">The new sync status.</param>
    /// <param name="errorMessage">Optional error message if sync failed.</param>
    void UpdatePromptSyncStatus(PromptSyncStatus status, string? errorMessage = null);
}

/// <summary>
/// Event arguments for menu state changes.
/// Contains the new revision number.
/// </summary>
public class MenuStateChangedEventArgs : EventArgs
{
    public uint Revision { get; }

    public MenuStateChangedEventArgs(uint revision)
    {
        Revision = revision;
    }
}
