namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Manages lifecycle of dependent services (e.g., TextToSpeech.Service).
/// </summary>
public interface IDependentServiceManager : IDisposable
{
    /// <summary>
    /// Starts all dependent services and begins monitoring their health.
    /// </summary>
    Task StartServicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops all dependent services gracefully.
    /// </summary>
    Task StopServicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of all dependent services.
    /// </summary>
    /// <returns>Dictionary of service name to running status.</returns>
    IDictionary<string, bool> GetServicesStatus();

    /// <summary>
    /// Refreshes the status of a specific service by checking its health endpoint.
    /// </summary>
    Task RefreshServiceStatusAsync(string serviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a specific dependent service.
    /// </summary>
    Task StartServiceAsync(string serviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a specific dependent service.
    /// </summary>
    Task StopServiceAsync(string serviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when a service status changes.
    /// </summary>
    event EventHandler<ServiceStatusChangedEventArgs>? ServiceStatusChanged;
}

/// <summary>
/// Event args for service status changes.
/// </summary>
public class ServiceStatusChangedEventArgs : EventArgs
{
    public string ServiceName { get; init; } = string.Empty;
    public bool IsRunning { get; init; }
}
