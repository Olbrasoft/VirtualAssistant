namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Service responsible for managing lifecycle of dependent systemd services.
/// Handles start/stop operations and status monitoring for external services.
/// NOTE: SpeechToText service methods removed (issue #466) - STT runs inline now.
/// </summary>
public interface IServiceLifecycleManager
{
    // NOTE: STT service methods removed (issue #466) - STT runs inline now

    /// <summary>
    /// Starts the log-viewer service.
    /// </summary>
    Task HandleStartLogViewerAsync();

    /// <summary>
    /// Stops the log-viewer service.
    /// </summary>
    Task HandleStopLogViewerAsync();

    /// <summary>
    /// Refreshes and updates log-viewer service status in menu.
    /// </summary>
    Task RefreshLogViewerStatusAsync();
}
