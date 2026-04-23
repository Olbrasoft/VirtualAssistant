using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.LinuxDesktop.Core.Models;
using Olbrasoft.LinuxDesktop.Core.Services;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;
using Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Workers.Dictation;

/// <summary>
/// Pins the "press fast from anywhere → text lands in the single Claude
/// Code window on this workspace" heuristic. The router's whole value is
/// knowing when NOT to switch focus — Text Editor scratchpad, ambiguous
/// multi-Claude layouts, and the already-on-target case must all fall
/// through silently so downstream paste continues unchanged.
/// </summary>
public class DictationFocusRouterTests
{
    private readonly Mock<IWindowQueryService> _windowQueryMock = new();
    private readonly Mock<IWindowActionService> _windowActionMock = new();
    private readonly Mock<ITerminalAgentIdentifier> _terminalAgentIdentifierMock = new();
    private readonly Mock<ILogger<DictationFocusRouter>> _loggerMock = new();

    private DictationFocusRouter CreateSut() =>
        new(_windowQueryMock.Object,
            _windowActionMock.Object,
            _terminalAgentIdentifierMock.Object,
            _loggerMock.Object);

    private static readonly KnownAgent ClaudeCodeAgent = CliAgentRegistry.KnownAgents
        .First(a => a.AppName == "Claude Code");

    private void SetupIdentifierReturns(int pid, KnownAgent? agent)
    {
        _terminalAgentIdentifierMock
            .Setup(x => x.IdentifyAsync(It.IsAny<string?>(), pid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);
    }

    private static WindowInfo MakeWindow(
        uint id,
        string wmClass,
        string title,
        bool inCurrentWorkspace = true,
        bool hasFocus = false,
        int pid = 0)
    {
        // LinuxDesktop.Core.Models.WindowInfo is init-only with a sizeable
        // surface. Tests only care about the fields the router inspects, so
        // we build it via object initializer and leave the rest at defaults.
        return new WindowInfo
        {
            Id = id,
            WmClass = wmClass,
            WmClassInstance = wmClass,
            Title = title,
            Pid = pid,
            InCurrentWorkspace = inCurrentWorkspace,
            HasFocus = hasFocus,
            FrameType = 0,
            WindowType = 0
        };
    }

    private void SetupWindows(params WindowInfo[] windows)
    {
        _windowQueryMock
            .Setup(x => x.GetWindowsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(windows);
    }

    [Fact]
    public async Task TryFocus_OneClaudeOnWorkspace_FocusedOnBrowser_Activates()
    {
        var claude = MakeWindow(42, "Alacritty", "Claude Code — ~/project", hasFocus: false);
        var browser = MakeWindow(7, "Google-chrome", "Desktop Monitor", hasFocus: true);
        SetupWindows(claude, browser);

        var switched = await CreateSut().TryFocusClaudeCodeIfApplicableAsync(CancellationToken.None);

        Assert.True(switched);
        _windowActionMock.Verify(x => x.ActivateWindowAsync(42u, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryFocus_FocusedOnTextEditor_SkipsEvenWithClaudeAvailable()
    {
        var claude = MakeWindow(42, "Alacritty", "Claude Code — ~/project");
        var textEditor = MakeWindow(9, "org.gnome.TextEditor", "Note (Koncept) – Textový editor", hasFocus: true);
        SetupWindows(claude, textEditor);

        var switched = await CreateSut().TryFocusClaudeCodeIfApplicableAsync(CancellationToken.None);

        Assert.False(switched);
        _windowActionMock.Verify(x => x.ActivateWindowAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryFocus_AlreadyFocusedOnClaude_DoesNotReactivate()
    {
        var claude = MakeWindow(42, "Alacritty", "Claude Code — ~/project", hasFocus: true);
        SetupWindows(claude);

        var switched = await CreateSut().TryFocusClaudeCodeIfApplicableAsync(CancellationToken.None);

        Assert.False(switched);
        _windowActionMock.Verify(x => x.ActivateWindowAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryFocus_NoClaudeOnWorkspace_Skips()
    {
        var browser = MakeWindow(7, "Google-chrome", "Browser", hasFocus: true);
        SetupWindows(browser);

        var switched = await CreateSut().TryFocusClaudeCodeIfApplicableAsync(CancellationToken.None);

        Assert.False(switched);
        _windowActionMock.Verify(x => x.ActivateWindowAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryFocus_MultipleClaudeOnWorkspace_SkipsAsAmbiguous()
    {
        var claudeA = MakeWindow(42, "Alacritty", "Claude Code — ~/project-a");
        var claudeB = MakeWindow(43, "Alacritty", "Claude Code — ~/project-b");
        var browser = MakeWindow(7, "Google-chrome", "Browser", hasFocus: true);
        SetupWindows(claudeA, claudeB, browser);

        var switched = await CreateSut().TryFocusClaudeCodeIfApplicableAsync(CancellationToken.None);

        Assert.False(switched);
        _windowActionMock.Verify(x => x.ActivateWindowAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryFocus_ClaudeOnOtherWorkspace_Skips()
    {
        // Claude Code exists but NOT on the current workspace — we must not
        // teleport the user to another workspace to paste.
        var claude = MakeWindow(42, "Alacritty", "Claude Code — ~/project", inCurrentWorkspace: false);
        var browser = MakeWindow(7, "Google-chrome", "Browser", hasFocus: true);
        SetupWindows(claude, browser);

        var switched = await CreateSut().TryFocusClaudeCodeIfApplicableAsync(CancellationToken.None);

        Assert.False(switched);
        _windowActionMock.Verify(x => x.ActivateWindowAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryFocus_WindowQueryThrows_SkipsSilently()
    {
        // D-Bus / window-calls extension missing or crashed is not fatal
        // for dictation — the caller should still paste, just without the
        // focus-switch boost.
        _windowQueryMock
            .Setup(x => x.GetWindowsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("D-Bus unavailable"));

        var switched = await CreateSut().TryFocusClaudeCodeIfApplicableAsync(CancellationToken.None);

        Assert.False(switched);
        _windowActionMock.Verify(x => x.ActivateWindowAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryFocus_WindowQueryCancelled_Propagates()
    {
        // Cancellation is a control-flow signal, not an error — pass it up so
        // the pipeline's finally block can unwind state-machine + feedback.
        _windowQueryMock
            .Setup(x => x.GetWindowsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => CreateSut().TryFocusClaudeCodeIfApplicableAsync(CancellationToken.None));
    }

    [Fact]
    public async Task TryFocus_ActivateWindowThrows_SkipsSilently()
    {
        // Activation failures (window closed mid-call, transient D-Bus error)
        // fall through same as query failures — Quick Dictation must keep
        // working against the currently-focused window.
        var claude = MakeWindow(42, "Alacritty", "Claude Code — ~/project");
        var browser = MakeWindow(7, "Google-chrome", "Browser", hasFocus: true);
        SetupWindows(claude, browser);
        _windowActionMock
            .Setup(x => x.ActivateWindowAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("window gone"));

        var switched = await CreateSut().TryFocusClaudeCodeIfApplicableAsync(CancellationToken.None);

        Assert.False(switched);
    }

    [Fact]
    public async Task TryFocus_ActivateCancelled_Propagates()
    {
        var claude = MakeWindow(42, "Alacritty", "Claude Code — ~/project");
        var browser = MakeWindow(7, "Google-chrome", "Browser", hasFocus: true);
        SetupWindows(claude, browser);
        _windowActionMock
            .Setup(x => x.ActivateWindowAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => CreateSut().TryFocusClaudeCodeIfApplicableAsync(CancellationToken.None));
    }

    [Fact]
    public async Task TryFocus_TerminatorWithShellTitleHostsClaudeInTmux_Activates()
    {
        // The real-world regression from #1056: terminator window shows title
        // "/bin/bash" because tmux does not propagate the Ink TUI title, but
        // a `claude` process (or `claude-*` tmux session) lives under its
        // PID. Title match fails → the identifier's process-tree / tmux path
        // must pick it up so the router still recognises Claude Code on this
        // workspace.
        var terminator = MakeWindow(
            id: 101,
            wmClass: "terminator",
            title: "/bin/bash",
            hasFocus: false,
            pid: 5000);
        var browser = MakeWindow(
            id: 7,
            wmClass: "Google-chrome",
            title: "Seznam – Edge",
            hasFocus: true,
            pid: 6000);
        SetupWindows(terminator, browser);
        SetupIdentifierReturns(terminator.Pid, ClaudeCodeAgent);

        var switched = await CreateSut().TryFocusClaudeCodeIfApplicableAsync(CancellationToken.None);

        Assert.True(switched);
        _windowActionMock.Verify(x => x.ActivateWindowAsync(101u, It.IsAny<CancellationToken>()), Times.Once);
        _terminalAgentIdentifierMock.Verify(
            x => x.IdentifyAsync("/bin/bash", terminator.Pid, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task TryFocus_TerminatorShellTitle_NoClaudeProcess_Skips()
    {
        // A terminator showing "/bin/bash" with nothing interesting under it
        // must NOT be treated as a Claude Code host — the identifier returns
        // null and the router falls through.
        var terminator = MakeWindow(101, "terminator", "/bin/bash", hasFocus: false, pid: 5000);
        var browser = MakeWindow(7, "Google-chrome", "Seznam – Edge", hasFocus: true, pid: 6000);
        SetupWindows(terminator, browser);
        SetupIdentifierReturns(terminator.Pid, null);

        var switched = await CreateSut().TryFocusClaudeCodeIfApplicableAsync(CancellationToken.None);

        Assert.False(switched);
        _windowActionMock.Verify(x => x.ActivateWindowAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryFocus_TitleAlreadyMatches_SkipsIdentifierProbe()
    {
        // Performance guard: when the terminal's outer title already contains
        // "Claude Code" (ws=3 in the user's setup), the router must not run
        // the pgrep-based identifier. Keeps the hot path cheap.
        var alacritty = MakeWindow(42, "Alacritty", "Claude Code — ~/project", pid: 5000);
        var browser = MakeWindow(7, "Google-chrome", "Browser", hasFocus: true, pid: 6000);
        SetupWindows(alacritty, browser);

        var switched = await CreateSut().TryFocusClaudeCodeIfApplicableAsync(CancellationToken.None);

        Assert.True(switched);
        _terminalAgentIdentifierMock.Verify(
            x => x.IdentifyAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryFocus_NonTerminalWithoutTitleMatch_SkipsIdentifierProbe()
    {
        // A Chrome window titled "My notes" is not a terminal and its title
        // doesn't match — the router must reject it without calling into the
        // process-tree code (which would waste pgrep cycles on a browser).
        var browser1 = MakeWindow(7, "Google-chrome", "My notes", hasFocus: true, pid: 6000);
        var browser2 = MakeWindow(8, "Google-chrome", "Another tab", pid: 6001);
        SetupWindows(browser1, browser2);

        var switched = await CreateSut().TryFocusClaudeCodeIfApplicableAsync(CancellationToken.None);

        Assert.False(switched);
        _terminalAgentIdentifierMock.Verify(
            x => x.IdentifyAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryFocus_IdentifierThrows_TreatsWindowAsNonClaude()
    {
        // Identifier failures (pgrep missing, process disappeared mid-walk)
        // must never kill the router. The window is logged as non-Claude and
        // paste falls through to the currently-focused window.
        var terminator = MakeWindow(101, "terminator", "/bin/bash", pid: 5000);
        var browser = MakeWindow(7, "Google-chrome", "Browser", hasFocus: true, pid: 6000);
        SetupWindows(terminator, browser);
        _terminalAgentIdentifierMock
            .Setup(x => x.IdentifyAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("pgrep failed"));

        var switched = await CreateSut().TryFocusClaudeCodeIfApplicableAsync(CancellationToken.None);

        Assert.False(switched);
        _windowActionMock.Verify(x => x.ActivateWindowAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryFocus_FocusedTerminatorNotClaude_IdentifierProbedOnce()
    {
        // Performance guard: when the focused window is a terminal (so the
        // "already on Claude?" check calls the identifier), the subsequent
        // candidate scan must not probe the focused window a second time —
        // a redundant process-tree walk on the hot path.
        var focusedTerm = MakeWindow(99, "terminator", "/bin/bash", hasFocus: true, pid: 4000);
        var otherTerm = MakeWindow(101, "terminator", "/bin/bash", pid: 5000);
        SetupWindows(focusedTerm, otherTerm);
        SetupIdentifierReturns(focusedTerm.Pid, agent: null);
        SetupIdentifierReturns(otherTerm.Pid, ClaudeCodeAgent);

        var switched = await CreateSut().TryFocusClaudeCodeIfApplicableAsync(CancellationToken.None);

        Assert.True(switched);
        _terminalAgentIdentifierMock.Verify(
            x => x.IdentifyAsync(It.IsAny<string?>(), focusedTerm.Pid, It.IsAny<CancellationToken>()),
            Times.Once);
        _windowActionMock.Verify(
            x => x.ActivateWindowAsync(otherTerm.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TryFocus_MultipleClaudeTerminators_ShortCircuitsAfterSecond()
    {
        // Once two Claude candidates are found, the outcome is already
        // "ambiguous, skip" — the loop must break before probing the rest
        // so a workspace with many terminators stays cheap.
        var browser = MakeWindow(7, "Google-chrome", "Browser", hasFocus: true, pid: 6000);
        var claude1 = MakeWindow(101, "terminator", "/bin/bash", pid: 5000);
        var claude2 = MakeWindow(102, "terminator", "/bin/bash", pid: 5001);
        var claude3 = MakeWindow(103, "terminator", "/bin/bash", pid: 5002);
        SetupWindows(browser, claude1, claude2, claude3);
        SetupIdentifierReturns(claude1.Pid, ClaudeCodeAgent);
        SetupIdentifierReturns(claude2.Pid, ClaudeCodeAgent);
        SetupIdentifierReturns(claude3.Pid, ClaudeCodeAgent);

        var switched = await CreateSut().TryFocusClaudeCodeIfApplicableAsync(CancellationToken.None);

        Assert.False(switched);
        _terminalAgentIdentifierMock.Verify(
            x => x.IdentifyAsync(It.IsAny<string?>(), claude3.Pid, It.IsAny<CancellationToken>()),
            Times.Never);
        _windowActionMock.Verify(
            x => x.ActivateWindowAsync(It.IsAny<uint>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryFocus_IdentifierCancelled_Propagates()
    {
        var terminator = MakeWindow(101, "terminator", "/bin/bash", pid: 5000);
        var browser = MakeWindow(7, "Google-chrome", "Browser", hasFocus: true, pid: 6000);
        SetupWindows(terminator, browser);
        _terminalAgentIdentifierMock
            .Setup(x => x.IdentifyAsync(It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => CreateSut().TryFocusClaudeCodeIfApplicableAsync(CancellationToken.None));
    }

    [Fact]
    public void Ctor_NullWindowQuery_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DictationFocusRouter(null!, _windowActionMock.Object, _terminalAgentIdentifierMock.Object, _loggerMock.Object));

    [Fact]
    public void Ctor_NullWindowAction_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DictationFocusRouter(_windowQueryMock.Object, null!, _terminalAgentIdentifierMock.Object, _loggerMock.Object));

    [Fact]
    public void Ctor_NullTerminalAgentIdentifier_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DictationFocusRouter(_windowQueryMock.Object, _windowActionMock.Object, null!, _loggerMock.Object));

    [Fact]
    public void Ctor_NullLogger_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new DictationFocusRouter(_windowQueryMock.Object, _windowActionMock.Object, _terminalAgentIdentifierMock.Object, null!));
}
