using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Service.Tray;
using Olbrasoft.VirtualAssistant.Service.Tray.Handlers;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Tray;

/// <summary>
/// After the #980 split <see cref="MenuEventDispatcher"/> is a thin facade
/// over four domain handlers — these tests pin the delegation contract.
/// Per-handler behavior (error swallowing, null-dependency guards, actual
/// side effects) is covered by the dedicated handler test classes.
/// </summary>
public class MenuEventDispatcherTests
{
    private readonly Mock<ILogger<MenuEventDispatcher>> _loggerMock = new();
    private readonly Mock<IMuteMenuHandler> _muteMock = new();
    private readonly Mock<IDictationMenuHandler> _dictationMock = new();
    private readonly Mock<ILlmMenuHandler> _llmMock = new();
    private readonly Mock<IDashboardMenuHandler> _dashboardMock = new();
    private readonly MenuEventDispatcher _sut;

    public MenuEventDispatcherTests()
    {
        _sut = new MenuEventDispatcher(
            _loggerMock.Object,
            _muteMock.Object,
            _dictationMock.Object,
            _llmMock.Object,
            _dashboardMock.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new MenuEventDispatcher(
            null!, _muteMock.Object, _dictationMock.Object, _llmMock.Object, _dashboardMock.Object));

    [Fact]
    public void Constructor_WithNullMute_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new MenuEventDispatcher(
            _loggerMock.Object, null!, _dictationMock.Object, _llmMock.Object, _dashboardMock.Object));

    [Fact]
    public void Constructor_WithNullDictation_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new MenuEventDispatcher(
            _loggerMock.Object, _muteMock.Object, null!, _llmMock.Object, _dashboardMock.Object));

    [Fact]
    public void Constructor_WithNullLlm_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new MenuEventDispatcher(
            _loggerMock.Object, _muteMock.Object, _dictationMock.Object, null!, _dashboardMock.Object));

    [Fact]
    public void Constructor_WithNullDashboard_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new MenuEventDispatcher(
            _loggerMock.Object, _muteMock.Object, _dictationMock.Object, _llmMock.Object, null!));

    [Fact]
    public void HandleMuteToggle_DelegatesToMuteHandler()
    {
        _sut.HandleMuteToggle();
        _muteMock.Verify(x => x.HandleMuteToggle(), Times.Once);
    }

    [Fact]
    public async Task HandleTtsMuteToggleAsync_DelegatesToMuteHandler()
    {
        _muteMock.Setup(x => x.HandleTtsMuteToggleAsync(true)).Returns(Task.CompletedTask);

        await _sut.HandleTtsMuteToggleAsync(true);

        _muteMock.Verify(x => x.HandleTtsMuteToggleAsync(true), Times.Once);
    }

    [Fact]
    public void HandleDashboard_DelegatesToDashboardHandler()
    {
        _sut.HandleDashboard();
        _dashboardMock.Verify(x => x.HandleDashboard(), Times.Once);
    }

    [Fact]
    public void HandleAbout_DelegatesToDashboardHandler()
    {
        _sut.HandleAbout();
        _dashboardMock.Verify(x => x.HandleAbout(), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HandleLlmCorrectionToggle_DelegatesToLlmHandler(bool enabled)
    {
        _sut.HandleLlmCorrectionToggle(enabled);
        _llmMock.Verify(x => x.HandleLlmCorrectionToggle(enabled), Times.Once);
    }

    [Fact]
    public void HandleReloadPrompt_DelegatesToLlmHandler()
    {
        _sut.HandleReloadPrompt();
        _llmMock.Verify(x => x.HandleReloadPrompt(), Times.Once);
    }

    [Fact]
    public void HandleMercuryBilling_DelegatesToLlmHandler()
    {
        _sut.HandleMercuryBilling();
        _llmMock.Verify(x => x.HandleMercuryBilling(), Times.Once);
    }

    [Fact]
    public void HandleReloadCorrectionsCache_DelegatesToLlmHandler()
    {
        _sut.HandleReloadCorrectionsCache();
        _llmMock.Verify(x => x.HandleReloadCorrectionsCache(), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HandleDictationToggle_DelegatesToDictationHandler(bool enabled)
    {
        _sut.HandleDictationToggle(enabled);
        _dictationMock.Verify(x => x.HandleDictationToggle(enabled), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HandleStreamingTranscriptionToggle_DelegatesToDictationHandler(bool enabled)
    {
        _sut.HandleStreamingTranscriptionToggle(enabled);
        _dictationMock.Verify(x => x.HandleStreamingTranscriptionToggle(enabled), Times.Once);
    }
}
