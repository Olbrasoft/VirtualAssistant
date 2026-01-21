using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.WindowManagement;

/// <summary>
/// Detects CLI applications running in terminal emulators by checking running processes.
/// This is useful when CLI apps (like Claude Code) change the terminal window title.
/// </summary>
public class TerminalCliAppDetector : ICliAppDetector
{
    private readonly ITerminalDetector _terminalDetector;
    private readonly ILogger<TerminalCliAppDetector> _logger;

    /// <summary>
    /// Known CLI applications to detect, mapped to their prompt configurations.
    /// Key: process name pattern, Value: (AppName, PromptFileName)
    /// </summary>
    private static readonly Dictionary<string, (string AppName, string PromptFileName)> KnownCliApps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude"] = ("Claude Code", "ClaudeCodeCorrection"),
        ["opencode"] = ("OpenCode", "OpenCodeCorrection")
    };

    public TerminalCliAppDetector(
        ITerminalDetector terminalDetector,
        ILogger<TerminalCliAppDetector> logger)
    {
        _terminalDetector = terminalDetector ?? throw new ArgumentNullException(nameof(terminalDetector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CliAppDetectionResult?> DetectCliAppAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Only check for CLI apps if a terminal is active
            if (!await _terminalDetector.IsTerminalActiveAsync(cancellationToken))
            {
                _logger.LogDebug("No terminal active, skipping CLI app detection");
                return null;
            }

            _logger.LogDebug("Terminal detected, checking for known CLI apps...");

            // Check each known CLI app
            foreach (var (processName, (appName, promptFileName)) in KnownCliApps)
            {
                if (await IsProcessRunningAsync(processName, cancellationToken))
                {
                    _logger.LogInformation("Detected CLI app: {AppName} (process: {ProcessName})", appName, processName);
                    return new CliAppDetectionResult(appName, promptFileName);
                }
            }

            _logger.LogDebug("No known CLI apps detected in terminal");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during CLI app detection");
            return null;
        }
    }

    /// <summary>
    /// Checks if a process with the given name is running.
    /// </summary>
    private async Task<bool> IsProcessRunningAsync(string processName, CancellationToken cancellationToken)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pgrep",
                    Arguments = $"-x {processName}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            // pgrep returns 0 if processes found, 1 if none found
            var isRunning = process.ExitCode == 0;
            _logger.LogDebug("Process check for '{ProcessName}': {IsRunning}", processName, isRunning);
            return isRunning;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to check if process '{ProcessName}' is running", processName);
            return false;
        }
    }
}
