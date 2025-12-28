namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Provides functionality to start services by spawning processes.
/// </summary>
public interface IProcessServiceStarter
{
    /// <summary>
    /// Starts a service by spawning a dotnet process.
    /// </summary>
    /// <param name="service">Service information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartAsync(DependentServiceInfo service, CancellationToken cancellationToken = default);
}
