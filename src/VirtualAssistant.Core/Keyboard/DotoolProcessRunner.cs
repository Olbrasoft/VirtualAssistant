using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.Keyboard;

/// <inheritdoc />
public sealed class DotoolProcessRunner : IDotoolProcessRunner
{
    private readonly ILogger<DotoolProcessRunner> _logger;

    public DotoolProcessRunner(ILogger<DotoolProcessRunner> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DotoolResult> SendKeysAsync(
        IReadOnlyList<string> keys,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count == 0) return DotoolResult.Ok();

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotool",
                RedirectStandardInput = true,
                // StandardOutput is intentionally NOT redirected. dotool produces
                // no meaningful output on success and leaving the child's stdout
                // attached to our stdout would let it deadlock if it ever wrote
                // more than the pipe buffer. (Copilot review on PR #997.)
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();
        foreach (var key in keys)
        {
            await process.StandardInput.WriteLineAsync($"key {key}".AsMemory(), cancellationToken);
        }
        process.StandardInput.Close();

        // Independent timeout CTS: binding the timer to cancellationToken would
        // conflate "user cancelled" with "dotool hung" and produce misleading
        // log messages.
        using var timeoutCts = new CancellationTokenSource(timeout);
        var processTask = process.WaitForExitAsync(cancellationToken);
        var timeoutTask = Task.Delay(Timeout.InfiniteTimeSpan, timeoutCts.Token);
        var done = await Task.WhenAny(processTask, timeoutTask);

        if (done == timeoutTask)
        {
            // Caller cancellation takes precedence over timer expiry so the
            // exception surfaced upstream reflects the real cause.
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogError("dotool timed out after {Timeout} — killing process", timeout);
            TryKill(process);
            return DotoolResult.Timeout();
        }

        try
        {
            await processTask;
        }
        catch (OperationCanceledException)
        {
            // Without this, a caller cancellation during wait would leave a
            // live dotool child behind — the regression Copilot flagged on
            // PR #997 relative to the prior FastPaste path.
            TryKill(process);
            throw;
        }

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            var message = string.IsNullOrWhiteSpace(error)
                ? $"dotool exited with code {process.ExitCode}."
                : $"dotool exited with code {process.ExitCode}: {error.TrimEnd()}";
            return DotoolResult.Failed(message);
        }

        return DotoolResult.Ok();
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill();
        }
        catch { /* best effort — process may have exited between the check and Kill */ }
    }
}
