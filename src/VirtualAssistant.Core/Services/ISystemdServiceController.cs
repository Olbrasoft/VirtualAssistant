namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Abstraction for controlling systemd services.
/// Follows OCP - allows different implementations (systemctl, mock, etc.).
/// </summary>
public interface ISystemdServiceController
{
    /// <summary>
    /// Start a systemd service.
    /// </summary>
    /// <param name="serviceName">Name of the service (e.g., "log-viewer.service")</param>
    /// <returns>True if start command succeeded</returns>
    Task<bool> StartServiceAsync(string serviceName);

    /// <summary>
    /// Stop a systemd service.
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <returns>True if stop command succeeded</returns>
    Task<bool> StopServiceAsync(string serviceName);

    /// <summary>
    /// Check if a systemd service is currently running.
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <returns>True if service is active/running</returns>
    Task<bool> IsRunningAsync(string serviceName);
}
