using System.Diagnostics;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Infrastructure;

/// <summary>
/// Implementation of ISystemdServiceController using systemctl commands.
/// </summary>
public class SystemdServiceController : ISystemdServiceController
{
    private readonly ILogger<SystemdServiceController> _logger;

    public SystemdServiceController(ILogger<SystemdServiceController> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> StartServiceAsync(string serviceName)
    {
        return await ExecuteSystemctlCommandAsync("start", serviceName);
    }

    /// <inheritdoc />
    public async Task<bool> StopServiceAsync(string serviceName)
    {
        return await ExecuteSystemctlCommandAsync("stop", serviceName);
    }

    /// <inheritdoc />
    public async Task<bool> IsRunningAsync(string serviceName)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "systemctl",
            Arguments = $"--user is-active {serviceName}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            _logger.LogWarning("Failed to start systemctl process to check status of {Service}", serviceName);
            return false;
        }

        await process.WaitForExitAsync();
        return process.ExitCode == 0;
    }

    /// <summary>
    /// Executes a systemctl command and returns whether it succeeded.
    /// </summary>
    private async Task<bool> ExecuteSystemctlCommandAsync(string command, string serviceName)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "systemctl",
            Arguments = $"--user {command} {serviceName}",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            _logger.LogError("Failed to start systemctl process for command '{Command} {Service}'",
                command, serviceName);
            return false;
        }

        await process.WaitForExitAsync();
        return process.ExitCode == 0;
    }
}
