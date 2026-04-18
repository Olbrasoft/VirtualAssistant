using Olbrasoft.VirtualAssistant.Core.WindowManagement;

namespace Olbrasoft.VirtualAssistant.Core.Tests.WindowManagement;

/// <summary>
/// Behavioural coverage for <see cref="CliAppDetectionCache"/>. The freshness
/// predicate is tested separately (see <c>TerminalCliAppDetectorCacheTests</c>);
/// here we pin the Set/TryGet/Clear contract.
/// </summary>
public class CliAppDetectionCacheTests
{
    [Fact]
    public void TryGet_BeforeAnySet_ReturnsNull()
    {
        var cache = new CliAppDetectionCache();

        Assert.Null(cache.TryGet("no cache yet"));
    }

    [Fact]
    public void TryGet_AfterSet_ReturnsStoredResult()
    {
        var cache = new CliAppDetectionCache();
        var stored = new CliAppDetectionResult("Claude Code", "ClaudeCodeCorrection");
        cache.Set(stored);

        var got = cache.TryGet("gdbus failed");

        Assert.NotNull(got);
        Assert.Equal("Claude Code", got!.AppName);
        Assert.Equal("ClaudeCodeCorrection", got.PromptFileName);
    }

    [Fact]
    public void TryGet_AfterClear_ReturnsNull()
    {
        var cache = new CliAppDetectionCache();
        cache.Set(new CliAppDetectionResult("Claude Code", "ClaudeCodeCorrection"));
        cache.Clear();

        Assert.Null(cache.TryGet("gdbus failed"));
    }

    [Fact]
    public void Set_Overwrites_PreviousValue()
    {
        // Detector transitions (Claude Code → OpenCode as user switches terminals)
        // must overwrite, not accumulate, so the stale app name never leaks back.
        var cache = new CliAppDetectionCache();
        cache.Set(new CliAppDetectionResult("Claude Code", "ClaudeCodeCorrection"));
        cache.Set(new CliAppDetectionResult("OpenCode", "OpenCodeCorrection"));

        var got = cache.TryGet("gdbus failed");

        Assert.Equal("OpenCode", got!.AppName);
    }
}
