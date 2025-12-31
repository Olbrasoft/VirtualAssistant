using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Configuration;

namespace Olbrasoft.VirtualAssistant.Service.Services;

/// <summary>
/// Manages lifecycle of dependent systemd services (log-viewer).
/// Implements Single Responsibility Principle - only handles service start/stop/status.
/// NOTE: SpeechToText service manager removed (issue #466) - STT runs inline now.
/// </summary>
public class ServiceLifecycleManager : IServiceLifecycleManager
{
    private readonly ILogger<ServiceLifecycleManager> _logger;
    private readonly IServiceStatusUpdater? _statusUpdater;
    private readonly ServiceMonitoringOptions _options;

    public ServiceLifecycleManager(
        ILogger<ServiceLifecycleManager> logger,
        IOptions<ServiceMonitoringOptions> options,
        IServiceStatusUpdater? statusUpdater = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options.Value;
        _statusUpdater = statusUpdater;
    }

    // NOTE: STT service methods removed (issue #466) - STT runs inline now

    /// <summary>
    /// Handles start log-viewer service request from menu.
    /// </summary>
    public async Task HandleStartLogViewerAsync()
    {
        try
        {
            _logger.LogInformation("Starting log-viewer service via tray menu");
            var success = await ExecuteSystemctlCommandAsync("start", "log-viewer.service");

            if (success)
            {
                // Poll for service to actually start
                await WaitForServiceStateAsync("log-viewer.service", expectedRunning: true);
                await RefreshLogViewerStatusAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start log-viewer service from tray menu");
        }
    }

    /// <summary>
    /// Handles stop log-viewer service request from menu.
    /// </summary>
    public async Task HandleStopLogViewerAsync()
    {
        try
        {
            _logger.LogInformation("Stopping log-viewer service via tray menu");
            var success = await ExecuteSystemctlCommandAsync("stop", "log-viewer.service");

            if (success)
            {
                // Poll for service to actually stop
                await WaitForServiceStateAsync("log-viewer.service", expectedRunning: false);
                await RefreshLogViewerStatusAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop log-viewer service from tray menu");
        }
    }

    /// <summary>
    /// Refreshes log-viewer service status and updates menu.
    /// </summary>
    public async Task RefreshLogViewerStatusAsync()
    {
        if (_statusUpdater == null)
            return;

        try
        {
            var isRunning = await CheckServiceIsRunningAsync("log-viewer.service");
            _statusUpdater.UpdateLogViewerStatus(isRunning);
            _logger.LogDebug("Log-viewer status updated: Running={IsRunning}", isRunning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh log-viewer status");
        }
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

    /// <summary>
    /// Checks if a systemd service is currently running.
    /// </summary>
    private async Task<bool> CheckServiceIsRunningAsync(string serviceName)
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
    /// Polls for service to reach expected state with timeout.
    /// Replaces fixed delays with actual state verification.
    /// </summary>
    private async Task WaitForServiceStateAsync(string serviceName, bool expectedRunning)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < _options.StatusPollTimeoutMs)
        {
            var isRunning = await CheckServiceIsRunningAsync(serviceName);
            if (isRunning == expectedRunning)
            {
                _logger.LogDebug("Service {Service} reached expected state (running={Expected}) after {ElapsedMs}ms",
                    serviceName, expectedRunning, sw.ElapsedMilliseconds);
                return;
            }

            await Task.Delay(_options.StatusPollIntervalMs);
        }

        _logger.LogWarning("Service {Service} did not reach expected state (running={Expected}) within {TimeoutMs}ms",
            serviceName, expectedRunning, _options.StatusPollTimeoutMs);
    }
}
