using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Services;

/// <summary>
/// Manages lifecycle of dependent systemd services (SpeechToText, log-viewer).
/// Implements Single Responsibility Principle - only handles service start/stop/status.
/// </summary>
public class ServiceLifecycleManager : IServiceLifecycleManager
{
    private readonly ILogger<ServiceLifecycleManager> _logger;
    private readonly ISpeechToTextServiceManager? _sttServiceManager;
    private readonly IServiceStatusUpdater? _statusUpdater;

    public ServiceLifecycleManager(
        ILogger<ServiceLifecycleManager> logger,
        ISpeechToTextServiceManager? sttServiceManager = null,
        IServiceStatusUpdater? statusUpdater = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sttServiceManager = sttServiceManager;
        _statusUpdater = statusUpdater;
    }

    /// <summary>
    /// Handles start SpeechToText service request from menu.
    /// </summary>
    public async Task HandleStartSpeechToTextAsync()
    {
        if (_sttServiceManager == null)
        {
            _logger.LogWarning("SpeechToTextServiceManager not available");
            return;
        }

        try
        {
            _logger.LogInformation("Starting SpeechToText.Service via tray menu");
            var success = await _sttServiceManager.StartAsync();

            if (success)
            {
                // Refresh status after starting
                await RefreshSpeechToTextStatusAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SpeechToText service from tray menu");
        }
    }

    /// <summary>
    /// Handles stop SpeechToText service request from menu.
    /// </summary>
    public async Task HandleStopSpeechToTextAsync()
    {
        if (_sttServiceManager == null)
        {
            _logger.LogWarning("SpeechToTextServiceManager not available");
            return;
        }

        try
        {
            _logger.LogInformation("Stopping SpeechToText.Service via tray menu");
            var success = await _sttServiceManager.StopAsync();

            if (success)
            {
                // Refresh status after stopping
                await RefreshSpeechToTextStatusAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop SpeechToText service from tray menu");
        }
    }

    /// <summary>
    /// Refreshes SpeechToText service status and updates menu.
    /// </summary>
    public async Task RefreshSpeechToTextStatusAsync()
    {
        if (_sttServiceManager == null || _statusUpdater == null)
            return;

        try
        {
            var isRunning = await _sttServiceManager.IsRunningAsync();
            var version = _sttServiceManager.GetVersion();
            _statusUpdater.UpdateSpeechToTextStatus(isRunning, version);
            _logger.LogDebug("SpeechToText status updated: Running={IsRunning}, Version={Version}",
                isRunning, version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh SpeechToText status");
        }
    }

    /// <summary>
    /// Handles start log-viewer service request from menu.
    /// </summary>
    public async Task HandleStartLogViewerAsync()
    {
        try
        {
            _logger.LogInformation("Starting log-viewer service via tray menu");

            var startInfo = new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = "--user start log-viewer.service",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();

                // Wait a bit for service to start
                await Task.Delay(500);

                // Refresh status
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

            var startInfo = new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = "--user stop log-viewer.service",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();

                // Wait a bit for service to stop
                await Task.Delay(500);

                // Refresh status
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
            // Check if service is running
            var startInfo = new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = "--user is-active log-viewer.service",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                var isRunning = process.ExitCode == 0;

                _statusUpdater.UpdateLogViewerStatus(isRunning);
                _logger.LogDebug("Log-viewer status updated: Running={IsRunning}", isRunning);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh log-viewer status");
        }
    }
}
