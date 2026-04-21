using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Core.WindowManagement;

/// <inheritdoc />
public sealed class TmuxCopyModeGuard : ITmuxCopyModeGuard
{
    private static readonly TimeSpan TmuxTimeout = TimeSpan.FromSeconds(2);

    // `tmux list-panes -a -F '<fmt>'` prints one record per pane. We use a
    // literal tab (ASCII 0x09) as the field separator because it cannot
    // appear in any of the scalar fields we emit.
    internal const string ListFormat = "#{pane_active}\t#{pane_in_mode}\t#{session_name}:#{window_index}.#{pane_index}";

    private readonly ILogger<TmuxCopyModeGuard> _logger;
    private readonly Func<IReadOnlyList<string>, CancellationToken, Task<string?>> _tmuxInvoker;

    public TmuxCopyModeGuard(ILogger<TmuxCopyModeGuard> logger)
        : this(logger, RunTmuxProcessAsync)
    {
    }

    // Test seam: inject a fake tmux runner so the unit test can drive the
    // parse + cancel-dispatch logic without shelling out to a real tmux.
    internal TmuxCopyModeGuard(
        ILogger<TmuxCopyModeGuard> logger,
        Func<IReadOnlyList<string>, CancellationToken, Task<string?>> tmuxInvoker)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tmuxInvoker = tmuxInvoker ?? throw new ArgumentNullException(nameof(tmuxInvoker));
    }

    public async Task EnsureNotInCopyModeAsync(CancellationToken cancellationToken)
    {
        List<string> stuckPanes;
        try
        {
            var listOutput = await _tmuxInvoker(
                new[] { "list-panes", "-a", "-F", ListFormat },
                cancellationToken);
            stuckPanes = ParseActivePanesInCopyMode(listOutput);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // tmux not installed / not running / transient IO error — caller's
            // paste path must still proceed. This guard is a best-effort pre-
            // step, never a blocker.
            _logger.LogDebug(ex, "TmuxCopyModeGuard: list-panes probe failed; skipping");
            return;
        }

        if (stuckPanes.Count == 0)
        {
            return;
        }

        foreach (var target in stuckPanes)
        {
            try
            {
                await _tmuxInvoker(
                    new[] { "send-keys", "-X", "-t", target, "cancel" },
                    cancellationToken);
                _logger.LogInformation("TmuxCopyModeGuard: sent 'cancel' to pane {Target} (was in copy-mode)", target);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "TmuxCopyModeGuard: failed to cancel copy-mode on {Target}", target);
            }
        }
    }

    /// <summary>
    /// Parses <c>tmux list-panes -a -F</c> output and returns every active
    /// pane that is currently in copy-mode. We only cancel active panes —
    /// touching an inactive pane would yank the user out of a scrollback
    /// they're reading and the paste never lands there anyway.
    /// </summary>
    internal static List<string> ParseActivePanesInCopyMode(string? listOutput)
    {
        var results = new List<string>();
        if (string.IsNullOrEmpty(listOutput))
        {
            return results;
        }

        foreach (var line in listOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t');
            if (parts.Length != 3)
            {
                continue;
            }

            var active = parts[0].Trim();
            var inMode = parts[1].Trim();
            var target = parts[2].Trim();

            if (active == "1" && inMode == "1" && !string.IsNullOrWhiteSpace(target))
            {
                results.Add(target);
            }
        }

        return results;
    }

    private static async Task<string?> RunTmuxProcessAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "tmux",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        foreach (var arg in arguments)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.Start();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TmuxTimeout);

        string output;
        try
        {
            output = await process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        return process.ExitCode == 0 ? output : null;
    }
}
