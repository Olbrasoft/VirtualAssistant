using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Clipboard;
using Olbrasoft.VirtualAssistant.Core.Keyboard;

namespace Olbrasoft.VirtualAssistant.Core.Tests.Keyboard;

/// <summary>
/// Unit tests for <see cref="ClipboardPasteOrchestrator"/>. The save/stage/restore
/// choreography used to be inlined inside every paste path of XDoToolKeyboardService
/// and was never unit-tested. Now that it's a separate class each branch is covered.
/// </summary>
public class ClipboardPasteOrchestratorTests
{
    private readonly Mock<IClipboardManager> _clipboardMock = new();
    private readonly Mock<ILogger<ClipboardPasteOrchestrator>> _loggerMock = new();
    private readonly ClipboardPasteOrchestrator _sut;

    public ClipboardPasteOrchestratorTests()
    {
        _sut = new ClipboardPasteOrchestrator(_clipboardMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task StageAndRestoreAsync_ClipboardPath_SavesStagesAndRestoresClipboard()
    {
        _clipboardMock.Setup(x => x.GetClipboardAsync(It.IsAny<CancellationToken>())).ReturnsAsync("original clipboard");
        var pasteRan = false;

        var result = await _sut.StageAndRestoreAsync(
            "new text",
            usePrimary: false,
            performPasteAsync: () => { pasteRan = true; return Task.FromResult(true); },
            CancellationToken.None);

        Assert.True(result);
        Assert.True(pasteRan);
        _clipboardMock.Verify(x => x.SetClipboardAsync("new text", It.IsAny<CancellationToken>()), Times.Once);
        _clipboardMock.Verify(x => x.SetClipboardAsync("original clipboard", It.IsAny<CancellationToken>()), Times.Once);
        _clipboardMock.Verify(x => x.GetPrimarySelectionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StageAndRestoreAsync_PrimaryPath_SavesStagesAndRestoresPrimary()
    {
        _clipboardMock.Setup(x => x.GetPrimarySelectionAsync(It.IsAny<CancellationToken>())).ReturnsAsync("original primary");

        var result = await _sut.StageAndRestoreAsync(
            "new text",
            usePrimary: true,
            performPasteAsync: () => Task.FromResult(true),
            CancellationToken.None);

        Assert.True(result);
        _clipboardMock.Verify(x => x.SetPrimarySelectionAsync("new text", It.IsAny<CancellationToken>()), Times.Once);
        _clipboardMock.Verify(x => x.SetPrimarySelectionAsync("original primary", It.IsAny<CancellationToken>()), Times.Once);
        _clipboardMock.Verify(x => x.GetClipboardAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StageAndRestoreAsync_EmptyOriginal_SkipsRestore()
    {
        // An empty original should not be "restored" — the legacy behavior was to
        // skip the SetClipboardAsync call when original was empty, and the test
        // guards against a future refactor that accidentally overwrites a blank
        // selection with another blank.
        _clipboardMock.Setup(x => x.GetClipboardAsync(It.IsAny<CancellationToken>())).ReturnsAsync(string.Empty);

        var result = await _sut.StageAndRestoreAsync(
            "new text",
            usePrimary: false,
            performPasteAsync: () => Task.FromResult(true),
            CancellationToken.None);

        Assert.True(result);
        _clipboardMock.Verify(x => x.SetClipboardAsync("new text", It.IsAny<CancellationToken>()), Times.Once);
        _clipboardMock.Verify(x => x.SetClipboardAsync(string.Empty, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StageAndRestoreAsync_PasteReturnsFalse_StillRestoresOriginal()
    {
        _clipboardMock.Setup(x => x.GetPrimarySelectionAsync(It.IsAny<CancellationToken>())).ReturnsAsync("original");

        var result = await _sut.StageAndRestoreAsync(
            "new text",
            usePrimary: true,
            performPasteAsync: () => Task.FromResult(false),
            CancellationToken.None);

        Assert.False(result);
        // Even when the paste action fails, the saved selection must be restored —
        // otherwise the user loses their previous clipboard/primary content.
        _clipboardMock.Verify(x => x.SetPrimarySelectionAsync("original", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StageAndRestoreAsync_PasteThrows_RestoresOriginalAndBubblesException()
    {
        _clipboardMock.Setup(x => x.GetClipboardAsync(It.IsAny<CancellationToken>())).ReturnsAsync("original");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.StageAndRestoreAsync(
            "new text",
            usePrimary: false,
            performPasteAsync: () => throw new InvalidOperationException("paste blew up"),
            CancellationToken.None));

        _clipboardMock.Verify(x => x.SetClipboardAsync("original", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StageAndRestoreAsync_RestoreFails_DoesNotMaskPasteResult()
    {
        // Restore failure must be swallowed and logged — it must not override
        // the caller's real success/failure result.
        _clipboardMock.Setup(x => x.GetPrimarySelectionAsync(It.IsAny<CancellationToken>())).ReturnsAsync("original");
        _clipboardMock
            .Setup(x => x.SetPrimarySelectionAsync("original", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("clipboard service down"));

        var result = await _sut.StageAndRestoreAsync(
            "new text",
            usePrimary: true,
            performPasteAsync: () => Task.FromResult(true),
            CancellationToken.None);

        Assert.True(result);
    }
}
