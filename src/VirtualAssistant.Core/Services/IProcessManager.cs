using System.Diagnostics;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Manages spawning and tracking processes.
/// </summary>
public interface IProcessManager
{
    /// <summary>
    /// Starts a dotnet run process for a project.
    /// </summary>
    /// <param name="projectPath">Path to the .csproj file.</param>
    /// <param name="serviceName">Name of the service (for logging).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The spawned process.</returns>
    Task<Process> StartDotnetRunAsync(
        string projectPath,
        string serviceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kills a process and waits for it to exit.
    /// </summary>
    /// <param name="process">The process to kill.</param>
    /// <param name="serviceName">Name of the service (for logging).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task KillProcessAsync(
        Process process,
        string serviceName,
        CancellationToken cancellationToken = default);
}
