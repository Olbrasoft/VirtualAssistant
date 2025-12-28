using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Finds and kills processes by port number.
/// Uses ss command to find processes listening on specific ports.
/// </summary>
public class PortBasedProcessKiller : IPortBasedProcessKiller
{
    private readonly ILogger<PortBasedProcessKiller> _logger;

    public PortBasedProcessKiller(ILogger<PortBasedProcessKiller> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Finds the process ID listening on a specific port.
    /// </summary>
    /// <param name="port">The port number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The process ID if found, null otherwise.</returns>
    public async Task<int?> FindProcessByPortAsync(int port, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use ss to find PID in LISTEN state on port (more reliable than lsof)
            // ss output: "users:(("TextToSpeech.Se",pid=607996,fd=247))"
            var ssProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ss",
                    Arguments = $"-tulpn sport = :{port}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            ssProcess.Start();
            var ssOutput = await ssProcess.StandardOutput.ReadToEndAsync(cancellationToken);
            await ssProcess.WaitForExitAsync(cancellationToken);

            // Parse ss output to find PID in LISTEN state
            // Example line: "tcp   LISTEN 0  512  127.0.0.1:5060  0.0.0.0:*  users:(("TextToSpeech.Se",pid=607996,fd=247))"
            foreach (var line in ssOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Contains("LISTEN") && line.Contains($":{port}"))
                {
                    // Extract PID from users:((process,pid=XXXXX,fd=...))
                    var match = Regex.Match(line, @"pid=(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var parsedPid))
                    {
                        _logger.LogInformation("Found process with PID {Pid} listening on port {Port}",
                            parsedPid, port);
                        return parsedPid;
                    }
                }
            }

            _logger.LogDebug("No process found listening on port {Port}", port);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find process by port {Port}", port);
            return null;
        }
    }

    /// <summary>
    /// Kills a process by its process ID.
    /// </summary>
    /// <param name="pid">The process ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the process was killed successfully, false otherwise.</returns>
    public async Task<bool> KillProcessByPidAsync(int pid, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Killing process with PID {Pid}", pid);

            var killProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "kill",
                    Arguments = $"-TERM {pid}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            killProcess.Start();
            await killProcess.WaitForExitAsync(cancellationToken);

            if (killProcess.ExitCode == 0)
            {
                _logger.LogInformation("Successfully killed process with PID {Pid}", pid);
                return true;
            }

            _logger.LogWarning("Failed to kill process with PID {Pid}, exit code: {ExitCode}",
                pid, killProcess.ExitCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to kill process with PID {Pid}", pid);
            return false;
        }
    }

    /// <summary>
    /// Finds and kills a process listening on a specific port.
    /// </summary>
    /// <param name="port">The port number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a process was found and killed, false otherwise.</returns>
    public async Task<bool> KillProcessByPortAsync(int port, CancellationToken cancellationToken = default)
    {
        var pid = await FindProcessByPortAsync(port, cancellationToken);
        if (pid.HasValue)
        {
            return await KillProcessByPidAsync(pid.Value, cancellationToken);
        }

        return false;
    }
}
