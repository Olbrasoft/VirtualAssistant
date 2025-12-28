using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Controls systemd user services (start, stop, check status).
/// Responsible for interacting with systemd via systemctl commands.
/// </summary>
public class SystemdServiceController : ISystemdServiceController
{
    private readonly ILogger<SystemdServiceController> _logger;

    public SystemdServiceController(ILogger<SystemdServiceController> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Checks if a systemd user service exists.
    /// </summary>
    /// <param name="serviceName">The systemd service name (e.g., "text-to-speech.service").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the service exists, false otherwise.</returns>
    public async Task<bool> ServiceExistsAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        try
        {
            var checkProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "systemctl",
                    Arguments = $"--user status {serviceName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            checkProcess.Start();
            await checkProcess.WaitForExitAsync(cancellationToken);

            // Exit code 4 = service not found, 3 = stopped, 0 = running
            return checkProcess.ExitCode != 4;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check if systemd service {ServiceName} exists", serviceName);
            return false;
        }
    }

    /// <summary>
    /// Starts a systemd user service.
    /// </summary>
    /// <param name="serviceName">The systemd service name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the service was started successfully, false otherwise.</returns>
    public async Task<bool> StartServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        try
        {
            var startProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "systemctl",
                    Arguments = $"--user start {serviceName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            startProcess.Start();
            await startProcess.WaitForExitAsync(cancellationToken);

            if (startProcess.ExitCode == 0)
            {
                _logger.LogInformation("Started systemd service {ServiceName}", serviceName);
                return true;
            }

            _logger.LogWarning("Failed to start systemd service {ServiceName}, exit code: {ExitCode}",
                serviceName, startProcess.ExitCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start systemd service {ServiceName}", serviceName);
            return false;
        }
    }

    /// <summary>
    /// Stops a systemd user service.
    /// </summary>
    /// <param name="serviceName">The systemd service name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the service was stopped successfully, false otherwise.</returns>
    public async Task<bool> StopServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        try
        {
            var stopProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "systemctl",
                    Arguments = $"--user stop {serviceName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            stopProcess.Start();
            await stopProcess.WaitForExitAsync(cancellationToken);

            if (stopProcess.ExitCode == 0)
            {
                _logger.LogInformation("Stopped systemd service {ServiceName}", serviceName);
                return true;
            }

            _logger.LogWarning("Failed to stop systemd service {ServiceName}, exit code: {ExitCode}",
                serviceName, stopProcess.ExitCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop systemd service {ServiceName}", serviceName);
            return false;
        }
    }
}
