namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Provides functionality to monitor service health.
/// </summary>
public interface IServiceHealthMonitor
{
    /// <summary>
    /// Checks if a service is healthy.
    /// </summary>
    /// <param name="service">Service information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if service is healthy, false otherwise.</returns>
    Task<bool> CheckHealthAsync(DependentServiceInfo service, CancellationToken cancellationToken = default);

    /// <summary>
    /// Continuously monitors services in the background.
    /// </summary>
    /// <param name="services">Services to monitor.</param>
    /// <param name="onStatusChanged">Callback when service status changes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MonitorAsync(
        IEnumerable<DependentServiceInfo> services,
        Action<string, bool> onStatusChanged,
        CancellationToken cancellationToken = default);
}
