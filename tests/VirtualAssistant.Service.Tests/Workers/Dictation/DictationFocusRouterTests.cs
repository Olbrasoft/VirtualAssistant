using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.LinuxDesktop.Core.Models;
using Olbrasoft.LinuxDesktop.Core.Services;
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
    private readonly Mock<ILogger<DictationFocusRouter>> _loggerMock = new();

    private DictationFocusRouter CreateSut() =>
        new(_windowQueryMock.Object, _windowActionMock.Object, _loggerMock.Object);

    private static WindowInfo MakeWindow(
        uint id,
        string wmClass,
        string title,
        bool inCurrentWorkspace = true,
        bool hasFocus = false)
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
            Pid = 0,
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
    public async Task Ctor_NullWindowQuery_Throws() =>
        await Task.Run(() => Assert.Throws<ArgumentNullException>(() =>
            new DictationFocusRouter(null!, _windowActionMock.Object, _loggerMock.Object)));

    [Fact]
    public async Task Ctor_NullWindowAction_Throws() =>
        await Task.Run(() => Assert.Throws<ArgumentNullException>(() =>
            new DictationFocusRouter(_windowQueryMock.Object, null!, _loggerMock.Object)));

    [Fact]
    public async Task Ctor_NullLogger_Throws() =>
        await Task.Run(() => Assert.Throws<ArgumentNullException>(() =>
            new DictationFocusRouter(_windowQueryMock.Object, _windowActionMock.Object, null!)));
}
