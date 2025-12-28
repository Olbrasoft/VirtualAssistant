namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Monitors service health via HTTP health check endpoints.
/// </summary>
public interface IServiceHealthMonitor
{
    /// <summary>
    /// Checks if a service is healthy by calling its health check endpoint.
    /// </summary>
    /// <param name="healthCheckUrl">The health check endpoint URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the service is healthy, false otherwise.</returns>
    Task<bool> CheckHealthAsync(string healthCheckUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Monitors services periodically and invokes callback when status changes.
    /// </summary>
    /// <param name="services">Dictionary of service names to health check URLs.</param>
    /// <param name="statusChangedCallback">Callback invoked when service status changes.</param>
    /// <param name="checkInterval">How often to check service health.</param>
    /// <param name="cancellationToken">Cancellation token to stop monitoring.</param>
    Task MonitorServicesAsync(
        IDictionary<string, string> services,
        Action<string, bool> statusChangedCallback,
        TimeSpan checkInterval,
        CancellationToken cancellationToken);
}
