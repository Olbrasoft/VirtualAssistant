using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Starts and stops services using systemd/systemctl.
/// </summary>
public class SystemdServiceStarter : ISystemdServiceStarter
{
    private readonly ILogger<SystemdServiceStarter> _logger;
    private readonly IProcessManager _processManager;

    public SystemdServiceStarter(
        ILogger<SystemdServiceStarter> logger,
        IProcessManager processManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
    }

    /// <inheritdoc/>
    public async Task<bool> TryStartAsync(DependentServiceInfo service, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if systemd service exists
            var checkProcess = _processManager.Start(new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = $"--user status {service.SystemdServiceName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (checkProcess == null)
                return false;

            await _processManager.WaitForExitAsync(checkProcess, cancellationToken);

            // Exit code 4 = service not found, 3 = stopped, 0 = running
            if (checkProcess.ExitCode == 4)
            {
                return false; // Service doesn't exist
            }

            // Try to start it
            var startProcess = _processManager.Start(new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = $"--user start {service.SystemdServiceName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (startProcess == null)
                return false;

            await _processManager.WaitForExitAsync(startProcess, cancellationToken);

            if (startProcess.ExitCode == 0)
            {
                _logger.LogInformation("Started {ServiceName} via systemd", service.Name);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start {ServiceName} via systemd", service.Name);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync(DependentServiceInfo service, CancellationToken cancellationToken = default)
    {
        try
        {
            var stopProcess = _processManager.Start(new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = $"--user stop {service.SystemdServiceName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (stopProcess != null)
            {
                await _processManager.WaitForExitAsync(stopProcess, cancellationToken);

                if (stopProcess.ExitCode == 0)
                {
                    _logger.LogInformation("Stopped {ServiceName} via systemd", service.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to stop {ServiceName} via systemd", service.Name);
        }
    }
}
