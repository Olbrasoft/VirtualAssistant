using Microsoft.Extensions.Logging.Abstractions;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;

namespace Olbrasoft.VirtualAssistant.Core.Tests.WindowManagement;

/// <summary>
/// Pins the parse + cancel-dispatch contract of <see cref="TmuxCopyModeGuard"/>.
/// The guard's whole job is: if ANY active pane is in copy-mode before a
/// VirtualAssistant paste, issue <c>send-keys -X cancel</c> so the upcoming
/// Shift+Insert is not hijacked by tmux bindings (#1050). Every failure mode
/// here — tmux not installed, list-panes empty, multiple panes stuck, inactive
/// pane also in copy-mode — has shown up in the wild at least once.
/// </summary>
public class TmuxCopyModeGuardTests
{
    [Fact]
    public void Parse_ActivePaneInCopyMode_Included()
    {
        var output = "1\t1\tclaude-ji:0.0";

        var result = TmuxCopyModeGuard.ParseActivePanesInCopyMode(output);

        Assert.Single(result);
        Assert.Equal("claude-ji:0.0", result[0]);
    }

    [Fact]
    public void Parse_InactivePaneInCopyMode_Excluded()
    {
        // Inactive pane is probably a scrollback another window left behind —
        // the user may be reading it. Don't cancel it, the paste won't go there.
        var output = "0\t1\tclaude-ji:0.0";

        var result = TmuxCopyModeGuard.ParseActivePanesInCopyMode(output);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_ActivePaneNotInCopyMode_Excluded()
    {
        var output = "1\t0\tclaude-ji:0.0";

        var result = TmuxCopyModeGuard.ParseActivePanesInCopyMode(output);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_MixedPanes_OnlyActiveCopyModeReturned()
    {
        // Real-world tmux list-panes -a on a host with several attached sessions.
        var output = string.Join('\n', new[]
        {
            "1\t0\tclaude-VirtualAssistant-pts-8:0.0",
            "1\t0\tclaude-cr-pts-4:0.0",
            "1\t1\tclaude-jirka-pts-2:0.0",
            "0\t1\tclaude-old-pts-99:0.0",
            "1\t0\tclaude-prehrajto-sync-pts-6:0.0",
        });

        var result = TmuxCopyModeGuard.ParseActivePanesInCopyMode(output);

        Assert.Single(result);
        Assert.Equal("claude-jirka-pts-2:0.0", result[0]);
    }

    [Fact]
    public void Parse_EmptyOutput_ReturnsEmpty()
    {
        var result = TmuxCopyModeGuard.ParseActivePanesInCopyMode(string.Empty);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_NullOutput_ReturnsEmpty()
    {
        var result = TmuxCopyModeGuard.ParseActivePanesInCopyMode(null);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_MalformedLine_Skipped()
    {
        // A tmux running an older format or a truncated line must not throw;
        // guard is a best-effort step.
        var output = string.Join('\n', new[]
        {
            "garbage",
            "1\t1\tclaude-ji:0.0",
            "1\t1",
            "",
        });

        var result = TmuxCopyModeGuard.ParseActivePanesInCopyMode(output);

        Assert.Single(result);
        Assert.Equal("claude-ji:0.0", result[0]);
    }

    [Fact]
    public async Task EnsureNotInCopyMode_NoStuckPanes_NoCancelSent()
    {
        var calls = new List<IReadOnlyList<string>>();
        var guard = CreateGuardWithFakeTmux(calls, listOutput: "1\t0\tclaude-ji:0.0");

        await guard.EnsureNotInCopyModeAsync(CancellationToken.None);

        // Exactly one call — the list-panes probe. No send-keys follow-up.
        Assert.Single(calls);
        Assert.Equal("list-panes", calls[0][0]);
    }

    [Fact]
    public async Task EnsureNotInCopyMode_OneStuckPane_CancelSentToThatTarget()
    {
        var calls = new List<IReadOnlyList<string>>();
        var guard = CreateGuardWithFakeTmux(calls, listOutput: "1\t1\tclaude-ji:0.0");

        await guard.EnsureNotInCopyModeAsync(CancellationToken.None);

        Assert.Equal(2, calls.Count);
        Assert.Equal("list-panes", calls[0][0]);
        Assert.Equal(new[] { "send-keys", "-X", "-t", "claude-ji:0.0", "cancel" }, calls[1]);
    }

    [Fact]
    public async Task EnsureNotInCopyMode_MultipleStuckPanes_CancelSentToEach()
    {
        var calls = new List<IReadOnlyList<string>>();
        var listOutput = string.Join('\n', new[]
        {
            "1\t1\tclaude-a:0.0",
            "1\t0\tclaude-b:0.0",
            "1\t1\tclaude-c:0.0",
        });
        var guard = CreateGuardWithFakeTmux(calls, listOutput);

        await guard.EnsureNotInCopyModeAsync(CancellationToken.None);

        Assert.Equal(3, calls.Count);
        Assert.Equal("claude-a:0.0", calls[1][3]);
        Assert.Equal("claude-c:0.0", calls[2][3]);
    }

    [Fact]
    public async Task EnsureNotInCopyMode_TmuxProbeThrows_Swallowed()
    {
        // tmux not installed / not running / transient crash must not propagate
        // to the paste pipeline — guard is advisory only.
        var guard = new TmuxCopyModeGuard(
            NullLogger<TmuxCopyModeGuard>.Instance,
            (_, _) => throw new InvalidOperationException("tmux missing"));

        await guard.EnsureNotInCopyModeAsync(CancellationToken.None);
    }

    [Fact]
    public async Task EnsureNotInCopyMode_CancelInvocationThrows_OtherTargetsStillAttempted()
    {
        // First send-keys fails, second must still be attempted so a user with
        // two stuck panes is not left with one still frozen.
        var listOutput = string.Join('\n', new[]
        {
            "1\t1\tclaude-a:0.0",
            "1\t1\tclaude-b:0.0",
        });
        var attempts = new List<string>();
        Task<string?> Runner(IReadOnlyList<string> args, CancellationToken _)
        {
            if (args[0] == "list-panes")
                return Task.FromResult<string?>(listOutput);

            var target = args[3];
            attempts.Add(target);
            if (target == "claude-a:0.0")
                throw new InvalidOperationException("tmux send-keys failed");
            return Task.FromResult<string?>("");
        }

        var guard = new TmuxCopyModeGuard(NullLogger<TmuxCopyModeGuard>.Instance, Runner);

        await guard.EnsureNotInCopyModeAsync(CancellationToken.None);

        Assert.Equal(new[] { "claude-a:0.0", "claude-b:0.0" }, attempts);
    }

    [Fact]
    public async Task EnsureNotInCopyMode_CancellationDuringProbe_Propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var guard = new TmuxCopyModeGuard(
            NullLogger<TmuxCopyModeGuard>.Instance,
            (_, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult<string?>(null);
            });

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => guard.EnsureNotInCopyModeAsync(cts.Token));
    }

    [Fact]
    public void Ctor_NullLogger_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new TmuxCopyModeGuard(null!));

    [Fact]
    public void Ctor_NullInvoker_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new TmuxCopyModeGuard(NullLogger<TmuxCopyModeGuard>.Instance, null!));

    private static TmuxCopyModeGuard CreateGuardWithFakeTmux(
        List<IReadOnlyList<string>> calls,
        string listOutput) =>
        new(
            NullLogger<TmuxCopyModeGuard>.Instance,
            (args, _) =>
            {
                calls.Add(args);
                return Task.FromResult<string?>(args[0] == "list-panes" ? listOutput : "");
            });
}
