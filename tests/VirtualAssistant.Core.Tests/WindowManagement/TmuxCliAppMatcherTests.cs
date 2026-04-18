using Olbrasoft.VirtualAssistant.Core.WindowManagement;

namespace Olbrasoft.VirtualAssistant.Core.Tests.WindowManagement;

/// <summary>
/// Covers the pure helpers that back the tmux-session fallback for the
/// detector. The helpers migrated to <see cref="TmuxCliAppMatcher"/> and
/// <see cref="CliAgentRegistry"/> during the #973 split; the tests followed
/// them. The full process-invoking path is tested manually.
/// </summary>
public class TmuxCliAppMatcherTests
{
    [Fact]
    public void ParseTmuxClients_EmptyOrNull_ReturnsEmptyMap()
    {
        Assert.Empty(TmuxCliAppMatcher.ParseTmuxClients(null));
        Assert.Empty(TmuxCliAppMatcher.ParseTmuxClients(""));
        Assert.Empty(TmuxCliAppMatcher.ParseTmuxClients("   \n  "));
    }

    [Fact]
    public void ParseTmuxClients_ThreeClientsRealOutput_ParsesAll()
    {
        var output = "78600:claude-VirtualAssistant\n98159:claude-vercel-pts-2\n106024:claude-streamtape-pts-3\n";

        var map = TmuxCliAppMatcher.ParseTmuxClients(output);

        Assert.Equal(3, map.Count);
        Assert.Equal("claude-VirtualAssistant", map[78600]);
        Assert.Equal("claude-vercel-pts-2", map[98159]);
        Assert.Equal("claude-streamtape-pts-3", map[106024]);
    }

    [Fact]
    public void ParseTmuxClients_SkipsMalformedLines()
    {
        var output = "12345:valid-session\nnot-a-pid:whatever\n:missing-pid\nnocolon-line\n67890:\n";

        var map = TmuxCliAppMatcher.ParseTmuxClients(output);

        Assert.Single(map);
        Assert.Equal("valid-session", map[12345]);
    }

    [Fact]
    public void ParseTmuxClients_SessionNameWithColon_PreservedAfterFirstSplit()
    {
        // Split on first ':' only so session names containing colons still parse.
        var output = "1001:weird:session:name\n";

        var map = TmuxCliAppMatcher.ParseTmuxClients(output);

        Assert.Equal("weird:session:name", map[1001]);
    }

    [Fact]
    public void MatchTmuxSessionName_ClaudePrefix_ReturnsClaudeCode()
    {
        var result = TmuxCliAppMatcher.MatchTmuxSessionName("claude-VirtualAssistant-pts-0");

        Assert.NotNull(result);
        Assert.Equal("Claude Code", result!.AppName);
        Assert.Equal("ClaudeCodeCorrection", result.PromptFileName);
    }

    [Fact]
    public void MatchTmuxSessionName_OpenCodePrefix_ReturnsOpenCode()
    {
        var result = TmuxCliAppMatcher.MatchTmuxSessionName("opencode-repo");

        Assert.NotNull(result);
        Assert.Equal("OpenCode", result!.AppName);
    }

    [Fact]
    public void MatchTmuxSessionName_GeminiPrefix_ReturnsGeminiCli()
    {
        var result = TmuxCliAppMatcher.MatchTmuxSessionName("gemini-foo");

        Assert.NotNull(result);
        Assert.Equal("Gemini CLI", result!.AppName);
    }

    [Fact]
    public void MatchTmuxSessionName_PrefixMatchIsCaseInsensitive()
    {
        // Session names come from tmux verbatim, but treat the match as case-
        // insensitive so a user who names a session "Claude-foo" still wins.
        var result = TmuxCliAppMatcher.MatchTmuxSessionName("Claude-Foo");

        Assert.NotNull(result);
        Assert.Equal("Claude Code", result!.AppName);
    }

    [Fact]
    public void MatchTmuxSessionName_UnknownPrefix_ReturnsNull()
    {
        Assert.Null(TmuxCliAppMatcher.MatchTmuxSessionName("work"));
        Assert.Null(TmuxCliAppMatcher.MatchTmuxSessionName("main"));
        Assert.Null(TmuxCliAppMatcher.MatchTmuxSessionName(""));
    }

    [Fact]
    public void MatchTmuxSessionName_PrefixMustBeAtStart_NotSubstring()
    {
        // "my-claude-session" contains "claude-" but not at the start — we
        // require the prefix so that the bashrc wrapper's naming stays the
        // single source of truth.
        Assert.Null(TmuxCliAppMatcher.MatchTmuxSessionName("my-claude-session"));
    }
}
