using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;

namespace Olbrasoft.VirtualAssistant.Core.Tests.WindowManagement;

/// <summary>
/// Unit tests cover the non-pgrep paths of <see cref="TerminalAgentIdentifier"/>:
/// title-marker match and null fallthrough. The process-tree path spawns
/// <c>pgrep -P</c> against live PIDs, so we point it at a PID that is
/// guaranteed not to exist — <c>pgrep</c> exits 1, the descendant set stays
/// empty, and the tmux matcher short-circuits on an empty set. That lets
/// us isolate title-match and no-match behaviour without depending on the
/// host's process list. The full three-way fallback is exercised manually
/// on the live system (same convention as the cache-only tests that cover
/// <see cref="TerminalCliAppDetector"/>).
/// </summary>
public class TerminalAgentIdentifierTests
{
    // pgrep -P <huge pid> exits 1 with no output, so GetDescendantProcessesAsync
    // returns an empty set deterministically — regardless of what the host is
    // actually running.
    private const int NonexistentPid = 999_999_999;

    private readonly Mock<ILogger<TerminalAgentIdentifier>> _loggerMock = new();
    private readonly Mock<ITmuxCliAppMatcher> _tmuxMatcherMock = new();

    private TerminalAgentIdentifier CreateSut() =>
        new(_loggerMock.Object, _tmuxMatcherMock.Object);

    [Fact]
    public async Task Identify_TitleContainsClaudeCode_ReturnsClaudeCodeAgent()
    {
        // No descendants → process-tree match fails → title path runs and
        // matches the "Claude Code" marker. The tmux matcher is never asked
        // because an empty descendant set already short-circuits it.
        var agent = await CreateSut().IdentifyAsync(
            "Claude Code — ~/project",
            NonexistentPid,
            CancellationToken.None);

        Assert.NotNull(agent);
        Assert.Equal("Claude Code", agent!.AppName);
    }

    [Fact]
    public async Task Identify_TitleContainsOpenCode_ReturnsOpenCodeAgent()
    {
        var agent = await CreateSut().IdentifyAsync(
            "OC | ~/project",
            NonexistentPid,
            CancellationToken.None);

        Assert.NotNull(agent);
        Assert.Equal("OpenCode", agent!.AppName);
    }

    [Fact]
    public async Task Identify_TitleBash_NoTmuxMatch_ReturnsNull()
    {
        _tmuxMatcherMock
            .Setup(x => x.MatchAsync(It.IsAny<IReadOnlySet<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CliAppDetectionResult?)null);

        var agent = await CreateSut().IdentifyAsync(
            "/bin/bash",
            NonexistentPid,
            CancellationToken.None);

        Assert.Null(agent);
    }

    [Fact]
    public async Task Identify_NullTitle_NoDescendants_ReturnsNull()
    {
        var agent = await CreateSut().IdentifyAsync(
            null,
            NonexistentPid,
            CancellationToken.None);

        Assert.Null(agent);
    }

    [Fact]
    public async Task Identify_CancellationRequested_Propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateSut().IdentifyAsync("/bin/bash", NonexistentPid, cts.Token));
    }

    [Fact]
    public void Ctor_NullLogger_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new TerminalAgentIdentifier(null!, _tmuxMatcherMock.Object));

    [Fact]
    public void Ctor_NullTmuxMatcher_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new TerminalAgentIdentifier(_loggerMock.Object, null!));
}
