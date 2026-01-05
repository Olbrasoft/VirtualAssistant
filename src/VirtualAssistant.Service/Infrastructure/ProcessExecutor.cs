using System.Diagnostics;
using System.Text;
using Olbrasoft.VirtualAssistant.Core.Processes;

namespace Olbrasoft.VirtualAssistant.Service.Infrastructure;

/// <summary>
/// Default implementation of <see cref="IProcessExecutor"/>.
/// Provides process execution capabilities with proper logging and error handling.
/// </summary>
public class ProcessExecutor : IProcessExecutor
{
    private readonly ILogger<ProcessExecutor> _logger;

    public ProcessExecutor(ILogger<ProcessExecutor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Process Start(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        _logger.LogDebug(
            "Starting process: {FileName} {Arguments}",
            startInfo.FileName,
            GetArgumentsForLogging(startInfo));

        var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException($"Failed to start process: {startInfo.FileName}");
        }

        _logger.LogDebug("Process started with PID: {ProcessId}", process.Id);
        return process;
    }

    /// <inheritdoc/>
    public async Task<ProcessExecutionResult> ExecuteAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        // Ensure output redirection is enabled
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;

        _logger.LogDebug(
            "Executing process: {FileName} {Arguments}",
            startInfo.FileName,
            GetArgumentsForLogging(startInfo));

        using var process = Start(startInfo);

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        // Read output and error streams asynchronously
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Process execution cancelled, killing process {ProcessId}", process.Id);

            try
            {
                // Double-check HasExited to avoid race condition
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    _logger.LogDebug("Process {ProcessId} killed successfully", process.Id);
                }
                else
                {
                    _logger.LogDebug("Process {ProcessId} already exited before kill", process.Id);
                }
            }
            catch (InvalidOperationException ex)
            {
                // Process exited between HasExited check and Kill call - this is expected
                _logger.LogDebug(ex, "Process {ProcessId} exited during kill attempt", process.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to kill process {ProcessId}", process.Id);
            }

            throw;
        }

        var stdout = outputBuilder.ToString().Trim();
        var stderr = errorBuilder.ToString().Trim();

        _logger.LogDebug(
            "Process {ProcessId} exited with code {ExitCode}",
            process.Id,
            process.ExitCode);

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
        {
            _logger.LogWarning(
                "Process {FileName} failed with exit code {ExitCode}: {Error}",
                startInfo.FileName,
                process.ExitCode,
                stderr);
        }

        return new ProcessExecutionResult(process.ExitCode, stdout, stderr);
    }

    /// <inheritdoc/>
    public async Task<bool> IsCommandAvailableAsync(
        string command,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "which",
                ArgumentList = { command },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var result = await ExecuteAsync(startInfo, cancellationToken);
            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to check if command '{Command}' is available", command);
            return false;
        }
    }

    /// <summary>
    /// Gets a safe representation of process arguments for logging.
    /// Handles both Arguments string and ArgumentList.
    /// </summary>
    private static string GetArgumentsForLogging(ProcessStartInfo startInfo)
    {
        if (startInfo.ArgumentList.Count > 0)
        {
            return string.Join(" ", startInfo.ArgumentList);
        }

        return startInfo.Arguments ?? string.Empty;
    }
}
