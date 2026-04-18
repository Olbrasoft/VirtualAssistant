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
                // StandardOutput is intentionally NOT redirected. The pipe-buffer
                // deadlock risk comes from redirecting without reading — if we
                // set RedirectStandardOutput = true and never drained the pipe,
                // dotool would block once its stdout buffer filled. Leaving it
                // attached to the parent is safe and matches the fact that dotool
                // has no meaningful output on success. (Copilot review on PR #997.)
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
            // Kill the child BEFORE anything that can throw. If caller cancellation
            // raced with the timer, the process must still die so we don't leak
            // a live dotool on the way out via the throw below. (Copilot review
            // on PR #998.)
            TryKill(process);

            // Caller cancellation takes precedence over timer expiry so the
            // exception surfaced upstream reflects the real cause.
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogError("dotool timed out after {Timeout}", timeout);
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
