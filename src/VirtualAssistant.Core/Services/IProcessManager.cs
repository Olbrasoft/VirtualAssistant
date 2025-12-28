using System.Diagnostics;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Provides abstraction over process management for testing.
/// </summary>
public interface IProcessManager
{
    /// <summary>
    /// Starts a new process with the specified information.
    /// </summary>
    /// <param name="startInfo">Process start information.</param>
    /// <returns>The started process, or null if start failed.</returns>
    Process? Start(ProcessStartInfo startInfo);

    /// <summary>
    /// Kills the specified process.
    /// </summary>
    /// <param name="process">Process to kill.</param>
    void Kill(Process process);

    /// <summary>
    /// Waits asynchronously for the process to exit.
    /// </summary>
    /// <param name="process">Process to wait for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WaitForExitAsync(Process process, CancellationToken cancellationToken = default);
}
