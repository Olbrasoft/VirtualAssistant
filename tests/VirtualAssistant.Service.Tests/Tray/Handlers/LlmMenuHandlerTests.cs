using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Service.Tray.Handlers;
using Olbrasoft.VirtualAssistant.Service.Tray.Menu;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Tray.Handlers;

public class LlmMenuHandlerTests
{
    private readonly Mock<ILogger<LlmMenuHandler>> _loggerMock = new();
    private readonly Mock<ILlmProvider> _llmProviderMock = new();
    private readonly Mock<IPromptSyncService> _syncMock = new();
    private readonly Mock<IMenuStateManager> _stateManagerMock = new();

    [Fact]
    public void HandleLlmCorrectionToggle_WithoutProvider_IsNoOp()
    {
        var sut = new LlmMenuHandler(_loggerMock.Object, llmProvider: null);

        var ex = Record.Exception(() => sut.HandleLlmCorrectionToggle(true));

        Assert.Null(ex);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HandleLlmCorrectionToggle_DelegatesToProvider(bool enabled)
    {
        var sut = new LlmMenuHandler(_loggerMock.Object, _llmProviderMock.Object);

        sut.HandleLlmCorrectionToggle(enabled);

        _llmProviderMock.Verify(x => x.SetEnabled(enabled), Times.Once);
    }

    [Fact]
    public void HandleReloadPrompt_WithSyncSuccess_UpdatesStateToInSync()
    {
        _syncMock.Setup(x => x.SyncPrompts()).Returns(new PromptSyncResult(true, 3, 0, Array.Empty<string>()));
        var sut = new LlmMenuHandler(_loggerMock.Object, _llmProviderMock.Object, _syncMock.Object, _stateManagerMock.Object);

        sut.HandleReloadPrompt();

        _llmProviderMock.Verify(x => x.ReloadPrompt(), Times.Once);
        _stateManagerMock.Verify(x => x.UpdatePromptSyncStatus(PromptSyncStatus.InSync, null), Times.Once);
    }

    [Fact]
    public void HandleReloadPrompt_WithSyncFailure_UpdatesStateToSyncFailed()
    {
        _syncMock.Setup(x => x.SyncPrompts()).Returns(new PromptSyncResult(false, 0, 1, new[] { "permission denied" }));
        var sut = new LlmMenuHandler(_loggerMock.Object, _llmProviderMock.Object, _syncMock.Object, _stateManagerMock.Object);

        sut.HandleReloadPrompt();

        _stateManagerMock.Verify(x => x.UpdatePromptSyncStatus(PromptSyncStatus.SyncFailed, "permission denied"), Times.Once);
        _llmProviderMock.Verify(x => x.ReloadPrompt(), Times.Never);
    }

    [Fact]
    public void HandleReloadPrompt_WithoutSyncService_FallsBackToCacheReload()
    {
        // Legacy deployment path where PromptSync isn't configured yet — the
        // menu click must still flush the LLM cache so the user can see effect.
        var sut = new LlmMenuHandler(_loggerMock.Object, _llmProviderMock.Object, promptSyncService: null, _stateManagerMock.Object);

        sut.HandleReloadPrompt();

        _llmProviderMock.Verify(x => x.ReloadPrompt(), Times.Once);
        _stateManagerMock.Verify(x => x.UpdatePromptSyncStatus(PromptSyncStatus.Unknown, null), Times.Once);
    }

    [Fact]
    public void HandleReloadCorrectionsCache_WithoutFilter_IsNoOp()
    {
        var sut = new LlmMenuHandler(_loggerMock.Object);

        var ex = Record.Exception(() => sut.HandleReloadCorrectionsCache());

        Assert.Null(ex);
    }
}
