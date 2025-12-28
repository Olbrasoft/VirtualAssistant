using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Manages spawning and tracking processes (e.g., dotnet run).
/// Responsible for starting processes, capturing output, and cleaning up.
/// </summary>
public class ProcessManager : IProcessManager
{
    private readonly ILogger<ProcessManager> _logger;

    public ProcessManager(ILogger<ProcessManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts a dotnet run process for a project.
    /// </summary>
    /// <param name="projectPath">Path to the .csproj file.</param>
    /// <param name="serviceName">Name of the service (for logging).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The spawned process.</returns>
    public Task<Process> StartDotnetRunAsync(
        string projectPath,
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project {projectPath}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(projectPath)
            };

            // Copy current environment variables to child process (required when UseShellExecute=false)
            foreach (System.Collections.DictionaryEntry envVar in Environment.GetEnvironmentVariables())
            {
                var key = envVar.Key?.ToString();
                var value = envVar.Value?.ToString();
                if (!string.IsNullOrEmpty(key) && value != null)
                {
                    startInfo.Environment[key] = value;
                }
            }

            var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    _logger.LogDebug("[{ServiceName}] {Output}", serviceName, e.Data);
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    _logger.LogWarning("[{ServiceName}] {Error}", serviceName, e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            _logger.LogInformation("Started {ServiceName} via dotnet run (PID: {ProcessId})",
                serviceName, process.Id);

            return Task.FromResult(process);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start {ServiceName} via dotnet run", serviceName);
            throw;
        }
    }

    /// <summary>
    /// Kills a process and waits for it to exit.
    /// </summary>
    /// <param name="process">The process to kill.</param>
    /// <param name="serviceName">Name of the service (for logging).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task KillProcessAsync(
        Process process,
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        if (process == null || process.HasExited)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Killing {ServiceName} process (PID: {ProcessId})",
                serviceName, process.Id);

            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken);
            process.Dispose();

            _logger.LogInformation("Successfully killed {ServiceName}", serviceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to kill {ServiceName} process", serviceName);
            throw;
        }
    }
}
