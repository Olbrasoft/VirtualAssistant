using System.Diagnostics;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Default implementation of process management.
/// </summary>
public class ProcessManager : IProcessManager
{
    /// <inheritdoc/>
    public Process? Start(ProcessStartInfo startInfo)
    {
        return Process.Start(startInfo);
    }

    /// <inheritdoc/>
    public void Kill(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }
    }

    /// <inheritdoc/>
    public async Task WaitForExitAsync(Process process, CancellationToken cancellationToken = default)
    {
        await process.WaitForExitAsync(cancellationToken);
    }
}
