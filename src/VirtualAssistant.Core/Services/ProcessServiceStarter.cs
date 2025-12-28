using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Starts services by spawning dotnet run processes.
/// </summary>
public class ProcessServiceStarter : IProcessServiceStarter
{
    private readonly ILogger<ProcessServiceStarter> _logger;
    private readonly IProcessManager _processManager;

    public ProcessServiceStarter(
        ILogger<ProcessServiceStarter> logger,
        IProcessManager processManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
    }

    /// <inheritdoc/>
    public Task StartAsync(DependentServiceInfo service, CancellationToken cancellationToken = default)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project {service.ProjectPath}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(service.ProjectPath)
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

            var process = _processManager.Start(startInfo);

            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start process for {service.Name}");
            }

            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    _logger.LogDebug("[{ServiceName}] {Output}", service.Name, e.Data);
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    _logger.LogWarning("[{ServiceName}] {Error}", service.Name, e.Data);
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            service.Process = process;

            _logger.LogInformation("Started {ServiceName} via dotnet run (PID: {ProcessId})",
                service.Name, process.Id);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start {ServiceName} via dotnet run", service.Name);
            throw;
        }
    }
}
