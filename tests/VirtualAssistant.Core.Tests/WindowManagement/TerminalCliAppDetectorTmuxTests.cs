using Olbrasoft.VirtualAssistant.Core.WindowManagement;

namespace Olbrasoft.VirtualAssistant.Core.Tests.WindowManagement;

/// <summary>
/// Covers the pure helpers that back the tmux-session fallback for
/// <see cref="TerminalCliAppDetector"/>. The full process-invoking path is
/// tested manually; the parser and prefix matcher are the interesting bits
/// because the bashrc wrapper's session naming (<c>claude-&lt;repo&gt;-&lt;tty&gt;</c>)
/// is what lets us detect claude inside tmux at all.
/// </summary>
public class TerminalCliAppDetectorTmuxTests
{
    [Fact]
    public void ParseTmuxClients_EmptyOrNull_ReturnsEmptyMap()
    {
        Assert.Empty(TerminalCliAppDetector.ParseTmuxClients(null));
        Assert.Empty(TerminalCliAppDetector.ParseTmuxClients(""));
        Assert.Empty(TerminalCliAppDetector.ParseTmuxClients("   \n  "));
    }

    [Fact]
    public void ParseTmuxClients_ThreeClientsRealOutput_ParsesAll()
    {
        var output = "78600:claude-VirtualAssistant\n98159:claude-vercel-pts-2\n106024:claude-streamtape-pts-3\n";

        var map = TerminalCliAppDetector.ParseTmuxClients(output);

        Assert.Equal(3, map.Count);
        Assert.Equal("claude-VirtualAssistant", map[78600]);
        Assert.Equal("claude-vercel-pts-2", map[98159]);
        Assert.Equal("claude-streamtape-pts-3", map[106024]);
    }

    [Fact]
    public void ParseTmuxClients_SkipsMalformedLines()
    {
        var output = "12345:valid-session\nnot-a-pid:whatever\n:missing-pid\nnocolon-line\n67890:\n";

        var map = TerminalCliAppDetector.ParseTmuxClients(output);

        Assert.Single(map);
        Assert.Equal("valid-session", map[12345]);
    }

    [Fact]
    public void ParseTmuxClients_SessionNameWithColon_PreservedAfterFirstSplit()
    {
        // Split on first ':' only so session names containing colons still parse.
        var output = "1001:weird:session:name\n";

        var map = TerminalCliAppDetector.ParseTmuxClients(output);

        Assert.Equal("weird:session:name", map[1001]);
    }

    [Fact]
    public void MatchTmuxSessionName_ClaudePrefix_ReturnsClaudeCode()
    {
        var result = TerminalCliAppDetector.MatchTmuxSessionName("claude-VirtualAssistant-pts-0");

        Assert.NotNull(result);
        Assert.Equal("Claude Code", result!.AppName);
        Assert.Equal("ClaudeCodeCorrection", result.PromptFileName);
    }

    [Fact]
    public void MatchTmuxSessionName_OpenCodePrefix_ReturnsOpenCode()
    {
        var result = TerminalCliAppDetector.MatchTmuxSessionName("opencode-repo");

        Assert.NotNull(result);
        Assert.Equal("OpenCode", result!.AppName);
    }

    [Fact]
    public void MatchTmuxSessionName_GeminiPrefix_ReturnsGeminiCli()
    {
        var result = TerminalCliAppDetector.MatchTmuxSessionName("gemini-foo");

        Assert.NotNull(result);
        Assert.Equal("Gemini CLI", result!.AppName);
    }

    [Fact]
    public void MatchTmuxSessionName_PrefixMatchIsCaseInsensitive()
    {
        // Session names come from tmux verbatim, but treat the match as case-
        // insensitive so a user who names a session "Claude-foo" still wins.
        var result = TerminalCliAppDetector.MatchTmuxSessionName("Claude-Foo");

        Assert.NotNull(result);
        Assert.Equal("Claude Code", result!.AppName);
    }

    [Fact]
    public void MatchTmuxSessionName_UnknownPrefix_ReturnsNull()
    {
        Assert.Null(TerminalCliAppDetector.MatchTmuxSessionName("work"));
        Assert.Null(TerminalCliAppDetector.MatchTmuxSessionName("main"));
        Assert.Null(TerminalCliAppDetector.MatchTmuxSessionName(""));
    }

    [Fact]
    public void MatchTmuxSessionName_PrefixMustBeAtStart_NotSubstring()
    {
        // "my-claude-session" contains "claude-" but not at the start — we
        // require the prefix so that the bashrc wrapper's naming stays the
        // single source of truth.
        Assert.Null(TerminalCliAppDetector.MatchTmuxSessionName("my-claude-session"));
    }
}
