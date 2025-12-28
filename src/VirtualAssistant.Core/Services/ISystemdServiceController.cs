namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Controls systemd user services.
/// </summary>
public interface ISystemdServiceController
{
    /// <summary>
    /// Checks if a systemd user service exists.
    /// </summary>
    /// <param name="serviceName">The systemd service name (e.g., "text-to-speech.service").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the service exists, false otherwise.</returns>
    Task<bool> ServiceExistsAsync(string serviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a systemd user service.
    /// </summary>
    /// <param name="serviceName">The systemd service name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the service was started successfully, false otherwise.</returns>
    Task<bool> StartServiceAsync(string serviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a systemd user service.
    /// </summary>
    /// <param name="serviceName">The systemd service name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the service was stopped successfully, false otherwise.</returns>
    Task<bool> StopServiceAsync(string serviceName, CancellationToken cancellationToken = default);
}
