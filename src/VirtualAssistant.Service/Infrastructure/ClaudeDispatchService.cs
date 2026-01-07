using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Core.Processes;
using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Infrastructure;

/// <summary>
/// Service for dispatching tasks to Claude Code via headless mode.
/// Orchestrates process execution, output parsing, and notifications.
/// </summary>
public class ClaudeDispatchService : IClaudeDispatchService
{
    private readonly ILogger<ClaudeDispatchService> _logger;
    private readonly ClaudeDispatchOptions _options;
    private readonly IProcessExecutor _processExecutor;
    private readonly IClaudeOutputParser _outputParser;
    private readonly IClaudeNotificationSender _notificationSender;

    public ClaudeDispatchService(
        ILogger<ClaudeDispatchService> logger,
        IOptions<ClaudeDispatchOptions> options,
        IProcessExecutor processExecutor,
        IClaudeOutputParser outputParser,
        IClaudeNotificationSender notificationSender)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _processExecutor = processExecutor ?? throw new ArgumentNullException(nameof(processExecutor));
        _outputParser = outputParser ?? throw new ArgumentNullException(nameof(outputParser));
        _notificationSender = notificationSender ?? throw new ArgumentNullException(nameof(notificationSender));
    }

    public async Task<ClaudeExecutionResult> ExecuteAsync(string prompt, string? workingDirectory = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var dir = workingDirectory ?? _options.GetExpandedWorkingDirectory();
        var timeout = TimeSpan.FromMinutes(_options.TimeoutMinutes);

        _logger.LogInformation(
            "Executing Claude headless mode in {Directory}: {Prompt}",
            dir, prompt.Length > 100 ? prompt[..100] + "..." : prompt);

        // Create timeout cancellation
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        Process? process = null;
        try
        {
            // Use ArgumentList instead of Arguments to prevent shell injection
            // ArgumentList handles all escaping automatically and safely
            var startInfo = new ProcessStartInfo
            {
                FileName = "claude",
                WorkingDirectory = dir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-p");
            startInfo.ArgumentList.Add(prompt);  // Safe - no manual escaping needed
            startInfo.ArgumentList.Add("--output-format");
            startInfo.ArgumentList.Add("json");

            process = _processExecutor.Start(startInfo);

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

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

            await process.WaitForExitAsync(linkedCts.Token);

            var output = outputBuilder.ToString().Trim();
            var error = errorBuilder.ToString().Trim();

            _logger.LogDebug("Claude exit code: {Code}, output length: {Len}", process.ExitCode, output.Length);

            if (process.ExitCode != 0)
            {
                _logger.LogError(
                    "Claude execution failed with exit code {Code}: {Error}",
                    process.ExitCode, error);
                await _notificationSender.NotifyErrorAsync($"Claude selhal s kódem {process.ExitCode}");
                return ClaudeExecutionResult.Failed(error, process.ExitCode);
            }

            // Parse JSON output
            var result = _outputParser.Parse(output);

            if (!result.Success)
            {
                await _notificationSender.NotifyErrorAsync($"Claude chyba: {result.Error}");
            }

            return result;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _logger.LogError("Claude execution timed out after {Timeout}", timeout);

            // Kill the process on timeout
            KillProcess(process);

            await _notificationSender.NotifyErrorAsync($"Claude timeout po {timeout.TotalMinutes} minutách");
            return ClaudeExecutionResult.Failed($"Timeout after {timeout.TotalMinutes} minutes", -1);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Claude execution was cancelled");
            KillProcess(process);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute Claude command");
            await _notificationSender.NotifyErrorAsync($"Claude selhání: {ex.Message}");
            return ClaudeExecutionResult.Failed(ex.Message);
        }
        finally
        {
            process?.Dispose();
        }
    }

    /// <summary>
    /// Kills the process and its children.
    /// </summary>
    private void KillProcess(Process? process)
    {
        if (process == null || process.HasExited)
            return;

        try
        {
            _logger.LogWarning("Killing Claude process {Pid}", process.Id);
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to kill Claude process");
        }
    }

    public async Task NotifySuccessAsync(string message)
    {
        await _notificationSender.NotifySuccessAsync(message);
    }

    public async Task<bool> IsClaudeAvailableAsync()
    {
        return await _processExecutor.IsCommandAvailableAsync("claude");
    }
}
