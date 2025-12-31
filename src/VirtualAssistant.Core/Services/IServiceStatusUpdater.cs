namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Interface for updating service status and state in tray menu.
/// </summary>
public interface IServiceStatusUpdater
{
    /// <summary>
    /// Updates mute state in menu.
    /// </summary>
    /// <param name="isMuted">Whether the assistant is muted</param>
    void UpdateMuteState(bool isMuted);

    /// <summary>
    /// Updates TTS mute state in menu.
    /// </summary>
    /// <param name="isMuted">Whether TTS is muted</param>
    void UpdateTtsMuteState(bool isMuted);

    /// <summary>
    /// Updates dictation status in menu.
    /// </summary>
    /// <param name="enabled">Whether dictation is enabled</param>
    void UpdateDictationStatus(bool enabled);

    /// <summary>
    /// Updates LLM correction status in menu.
    /// </summary>
    /// <param name="enabled">Whether LLM correction is enabled</param>
    void UpdateLlmCorrectionStatus(bool enabled);

    // NOTE: UpdateSpeechToTextStatus removed (issue #466) - STT runs inline now

    /// <summary>
    /// Updates log-viewer service status in menu.
    /// </summary>
    /// <param name="isRunning">Whether the service is running</param>
    void UpdateLogViewerStatus(bool isRunning);
}
