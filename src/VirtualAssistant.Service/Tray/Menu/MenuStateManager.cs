namespace Olbrasoft.VirtualAssistant.Service.Tray.Menu;

/// <summary>
/// Manages the state of all menu items.
/// Tracks mute status, service statuses, and feature toggles.
/// Notifies listeners when state changes via StateChanged event.
/// </summary>
public class MenuStateManager : IMenuStateManager
{
    private bool _isMuted;
    private bool _ttsMuted;
    private string _logViewerStatus = "Checking...";
    private bool _llmCorrectionEnabled = true;
    private bool _dictationEnabled = true;
    private PromptSyncStatus _promptSyncStatus = PromptSyncStatus.Unknown;
    private string? _lastSyncError;
    private uint _revision;

    /// <summary>
    /// Initializes a new instance of MenuStateManager with revision starting at 1.
    /// </summary>
    public MenuStateManager()
    {
        _revision = 1;
    }

    /// <summary>
    /// Event raised when menu state changes and layout needs to be refreshed.
    /// </summary>
    public event EventHandler<MenuStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Gets the current revision number for D-Bus menu updates.
    /// </summary>
    public uint Revision => _revision;

    /// <summary>
    /// Gets whether the assistant is muted.
    /// </summary>
    public bool IsMuted => _isMuted;

    /// <summary>
    /// Gets whether TTS is muted.
    /// </summary>
    public bool IsTtsMuted => _ttsMuted;

    /// <summary>
    /// Gets the current status of the log viewer service.
    /// </summary>
    public string LogViewerStatus => _logViewerStatus;

    /// <summary>
    /// Gets whether LLM correction is enabled.
    /// </summary>
    public bool IsLlmCorrectionEnabled => _llmCorrectionEnabled;

    /// <summary>
    /// Gets whether dictation is enabled.
    /// </summary>
    public bool IsDictationEnabled => _dictationEnabled;

    /// <summary>
    /// Gets the current prompt synchronization status.
    /// </summary>
    public PromptSyncStatus PromptSyncStatus => _promptSyncStatus;

    /// <summary>
    /// Gets the error message from the last failed sync, if any.
    /// </summary>
    public string? LastSyncError => _lastSyncError;

    /// <summary>
    /// Updates the mute state.
    /// </summary>
    public void UpdateMuteState(bool isMuted)
    {
        _isMuted = isMuted;
        IncrementRevisionAndNotify();
    }

    /// <summary>
    /// Updates the TTS mute state.
    /// </summary>
    public void UpdateTtsMuteState(bool isMuted)
    {
        _ttsMuted = isMuted;
        IncrementRevisionAndNotify();
    }

    /// <summary>
    /// Updates the log viewer service status.
    /// </summary>
    public void UpdateLogViewerStatus(bool isRunning)
    {
        _logViewerStatus = isRunning ? "Running" : "Stopped";
        IncrementRevisionAndNotify();
    }

    /// <summary>
    /// Updates the LLM correction enabled status.
    /// </summary>
    public void UpdateLlmCorrectionStatus(bool enabled)
    {
        _llmCorrectionEnabled = enabled;
        IncrementRevisionAndNotify();
    }

    /// <summary>
    /// Updates the dictation enabled status.
    /// </summary>
    public void UpdateDictationStatus(bool enabled)
    {
        _dictationEnabled = enabled;
        IncrementRevisionAndNotify();
    }

    /// <summary>
    /// Updates the prompt synchronization status.
    /// </summary>
    /// <param name="status">The new sync status.</param>
    /// <param name="errorMessage">Optional error message if sync failed.</param>
    public void UpdatePromptSyncStatus(PromptSyncStatus status, string? errorMessage = null)
    {
        _promptSyncStatus = status;
        _lastSyncError = status == PromptSyncStatus.SyncFailed ? errorMessage : null;
        IncrementRevisionAndNotify();
    }

    /// <summary>
    /// Increments the revision counter and notifies listeners of state change.
    /// </summary>
    private void IncrementRevisionAndNotify()
    {
        _revision++;
        StateChanged?.Invoke(this, new MenuStateChangedEventArgs(_revision));
    }
}
