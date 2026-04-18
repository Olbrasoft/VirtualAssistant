using Olbrasoft.VirtualAssistant.Core.WindowManagement;

namespace Olbrasoft.VirtualAssistant.Core.Tests.WindowManagement;

/// <summary>
/// Coverage for the cross-strategy lookup helpers on <see cref="CliAgentRegistry"/>.
/// The tmux-prefix lookup is exercised indirectly via <c>TmuxCliAppMatcher</c>
/// tests; here we pin the title-marker path that the detector's orchestrator
/// uses for the non-tmux title fallback.
/// </summary>
public class CliAgentRegistryTests
{
    [Fact]
    public void FindByTitle_ClaudeCodeMarkerAnywhereInTitle_ReturnsAgent()
    {
        var agent = CliAgentRegistry.FindByTitle("my-repo — Claude Code");

        Assert.NotNull(agent);
        Assert.Equal("Claude Code", agent!.AppName);
    }

    [Fact]
    public void FindByTitle_OpenCodeShorthand_Matches()
    {
        // OpenCode sets "OC | ..." in some terminals — the shorthand is one of
        // the declared markers and must not fall through to "no match".
        var agent = CliAgentRegistry.FindByTitle("OC | main");

        Assert.NotNull(agent);
        Assert.Equal("OpenCode", agent!.AppName);
    }

    [Fact]
    public void FindByTitle_CaseInsensitive()
    {
        var agent = CliAgentRegistry.FindByTitle("CLAUDE CODE");

        Assert.NotNull(agent);
        Assert.Equal("Claude Code", agent!.AppName);
    }

    [Fact]
    public void FindByTitle_NoKnownMarker_ReturnsNull()
    {
        Assert.Null(CliAgentRegistry.FindByTitle("bash - 80x24"));
        Assert.Null(CliAgentRegistry.FindByTitle(""));
        Assert.Null(CliAgentRegistry.FindByTitle(null));
    }

    [Fact]
    public void FindByTmuxSession_PrefixMustBeAtStart()
    {
        Assert.Null(CliAgentRegistry.FindByTmuxSession("work-claude-"));
        Assert.NotNull(CliAgentRegistry.FindByTmuxSession("claude-work"));
    }
}
