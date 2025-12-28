namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Provides functionality to start and stop services via systemd.
/// </summary>
public interface ISystemdServiceStarter
{
    /// <summary>
    /// Attempts to start a service via systemd.
    /// </summary>
    /// <param name="service">Service information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if service was started successfully, false otherwise.</returns>
    Task<bool> TryStartAsync(DependentServiceInfo service, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a service via systemd.
    /// </summary>
    /// <param name="service">Service information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StopAsync(DependentServiceInfo service, CancellationToken cancellationToken = default);
}
