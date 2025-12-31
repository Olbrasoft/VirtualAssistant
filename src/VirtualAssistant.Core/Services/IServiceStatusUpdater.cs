namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Interface for updating service status in tray menu.
/// </summary>
public interface IServiceStatusUpdater
{
    /// <summary>
    /// Updates SpeechToText service status in menu.
    /// </summary>
    /// <param name="isRunning">Whether the service is running</param>
    /// <param name="version">Service version</param>
    void UpdateSpeechToTextStatus(bool isRunning, string version);

    /// <summary>
    /// Updates log-viewer service status in menu.
    /// </summary>
    /// <param name="isRunning">Whether the service is running</param>
    void UpdateLogViewerStatus(bool isRunning);
}
