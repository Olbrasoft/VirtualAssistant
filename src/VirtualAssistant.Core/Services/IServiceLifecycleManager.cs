namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Service responsible for managing lifecycle of dependent systemd services.
/// Handles start/stop operations and status monitoring for external services.
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
    /// Refreshes and updates SpeechToText service status in menu.
    /// </summary>
    Task RefreshSpeechToTextStatusAsync();

    /// <summary>
    /// Refreshes and updates log-viewer service status in menu.
    /// </summary>
    Task RefreshLogViewerStatusAsync();
}
