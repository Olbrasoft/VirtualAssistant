namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Finds and kills processes by port number.
/// </summary>
public interface IPortBasedProcessKiller
{
    /// <summary>
    /// Finds the process ID listening on a specific port.
    /// </summary>
    /// <param name="port">The port number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The process ID if found, null otherwise.</returns>
    Task<int?> FindProcessByPortAsync(int port, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kills a process by its process ID.
    /// </summary>
    /// <param name="pid">The process ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the process was killed successfully, false otherwise.</returns>
    Task<bool> KillProcessByPidAsync(int pid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds and kills a process listening on a specific port.
    /// </summary>
    /// <param name="port">The port number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a process was found and killed, false otherwise.</returns>
    Task<bool> KillProcessByPortAsync(int port, CancellationToken cancellationToken = default);
}
