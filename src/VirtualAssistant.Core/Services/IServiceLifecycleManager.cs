namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Manages lifecycle of external systemd services (SpeechToText, LogViewer).
/// Implements Single Responsibility Principle - only handles service lifecycle.
/// </summary>
public interface IServiceLifecycleManager
{
    /// <summary>
    /// Starts the SpeechToText service.
    /// </summary>
    Task HandleStartSpeechToTextAsync();

    /// <summary>
    /// Stops the SpeechToText service.
    /// </summary>
    Task HandleStopSpeechToTextAsync();

    /// <summary>
    /// Starts the log-viewer service.
    /// </summary>
    Task HandleStartLogViewerAsync();

    /// <summary>
    /// Stops the log-viewer service.
    /// </summary>
    Task HandleStopLogViewerAsync();

    /// <summary>
    /// Refreshes SpeechToText service status.
    /// </summary>
    Task RefreshSpeechToTextStatusAsync();

    /// <summary>
    /// Refreshes log-viewer service status.
    /// </summary>
    Task RefreshLogViewerStatusAsync();
}
