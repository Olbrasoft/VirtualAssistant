namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Provides functionality to manage service lifecycle (start/stop).
/// </summary>
public interface IServiceLifecycleManager
{
    /// <summary>
    /// Starts a service, trying systemd first, then dotnet run.
    /// </summary>
    /// <param name="service">Service information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartServiceAsync(DependentServiceInfo service, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a running service.
    /// </summary>
    /// <param name="service">Service information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StopServiceAsync(DependentServiceInfo service, CancellationToken cancellationToken = default);
}
