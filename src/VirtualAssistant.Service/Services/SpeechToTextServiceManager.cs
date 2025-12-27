using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Service.Services;

/// <summary>
/// Manages SpeechToText microservice - checks status, starts/stops service, gets version.
/// </summary>
public class SpeechToTextServiceManager
{
    private readonly ILogger<SpeechToTextServiceManager> _logger;
    private const string ServiceName = "speech-to-text.service";

    public SpeechToTextServiceManager(ILogger<SpeechToTextServiceManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Checks if SpeechToText service is running.
    /// </summary>
    public async Task<bool> IsRunningAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = $"--user is-active {ServiceName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return false;

            await process.WaitForExitAsync();
            var output = await process.StandardOutput.ReadToEndAsync();

            return output.Trim() == "active";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check SpeechToText service status");
            return false;
        }
    }

    /// <summary>
    /// Gets SpeechToText service version from deployed binary.
    /// </summary>
    public string GetVersion()
    {
        try
        {
            var binaryPath = "/opt/olbrasoft/speech-to-text/app/SpeechToText.Service.dll";
            if (!File.Exists(binaryPath))
                return "Unknown";

            var versionInfo = FileVersionInfo.GetVersionInfo(binaryPath);
            return versionInfo.FileVersion ?? "Unknown";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get SpeechToText service version");
            return "Unknown";
        }
    }

    /// <summary>
    /// Starts SpeechToText service.
    /// </summary>
    public async Task<bool> StartAsync()
    {
        try
        {
            _logger.LogInformation("Starting SpeechToText service...");

            var psi = new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = $"--user start {ServiceName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return false;

            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("SpeechToText service started successfully");
                return true;
            }
            else
            {
                var error = await process.StandardError.ReadToEndAsync();
                _logger.LogError("Failed to start SpeechToText service: {Error}", error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while starting SpeechToText service");
            return false;
        }
    }

    /// <summary>
    /// Stops SpeechToText service.
    /// </summary>
    public async Task<bool> StopAsync()
    {
        try
        {
            _logger.LogInformation("Stopping SpeechToText service...");

            var psi = new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = $"--user stop {ServiceName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return false;

            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                _logger.LogInformation("SpeechToText service stopped successfully");
                return true;
            }
            else
            {
                var error = await process.StandardError.ReadToEndAsync();
                _logger.LogError("Failed to stop SpeechToText service: {Error}", error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while stopping SpeechToText service");
            return false;
        }
    }
}
