using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Service.Tray.Menu;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Tray;

public class MenuEventRouterTests
{
    private readonly Mock<ILogger<MenuEventRouter>> _loggerMock;
    private readonly Mock<IMenuStateManager> _stateManagerMock;
    private readonly MenuEventRouter _router;

    public MenuEventRouterTests()
    {
        _loggerMock = new Mock<ILogger<MenuEventRouter>>();
        _stateManagerMock = new Mock<IMenuStateManager>();
        _router = new MenuEventRouter(_loggerMock.Object, _stateManagerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new MenuEventRouter(null!, _stateManagerMock.Object));
        Assert.Equal("logger", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithNullStateManager_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new MenuEventRouter(_loggerMock.Object, null!));
        Assert.Equal("stateManager", ex.ParamName);
    }

    #endregion

    #region HandleMenuEvent - Event Filtering

    [Fact]
    public void HandleMenuEvent_WithNonClickedEvent_DoesNothing()
    {
        var eventFired = false;
        _router.OnQuitRequested += () => eventFired = true;

        _router.HandleMenuEvent(MenuItemIds.QuitId, "hovered");

        Assert.False(eventFired);
    }

    [Theory]
    [InlineData("hovered")]
    [InlineData("")]
    [InlineData("activated")]
    public void HandleMenuEvent_WithNonClickedEvents_IgnoresEvent(string eventId)
    {
        var quitFired = false;
        _router.OnQuitRequested += () => quitFired = true;

        _router.HandleMenuEvent(MenuItemIds.QuitId, eventId);

        Assert.False(quitFired);
    }

    #endregion

    #region HandleMenuEvent - Quit

    [Fact]
    public void HandleMenuEvent_QuitClicked_FiresOnQuitRequested()
    {
        var eventFired = false;
        _router.OnQuitRequested += () => eventFired = true;

        _router.HandleMenuEvent(MenuItemIds.QuitId, "clicked");

        Assert.True(eventFired);
    }

    #endregion

    #region HandleMenuEvent - MuteToggle

    [Fact]
    public void HandleMenuEvent_MuteToggleClicked_FiresOnMuteToggleRequested()
    {
        var eventFired = false;
        _router.OnMuteToggleRequested += () => eventFired = true;

        _router.HandleMenuEvent(MenuItemIds.MuteToggleId, "clicked");

        Assert.True(eventFired);
    }

    #endregion

    #region HandleMenuEvent - TtsMuteToggle

    [Fact]
    public void HandleMenuEvent_TtsMuteToggleClicked_WhenUnmuted_TogglesAndFiresEvent()
    {
        _stateManagerMock.Setup(s => s.IsTtsMuted).Returns(false);
        bool? newState = null;
        _router.OnTtsMuteToggleRequested += state => newState = state;

        _router.HandleMenuEvent(MenuItemIds.TtsMuteToggleId, "clicked");

        _stateManagerMock.Verify(s => s.UpdateTtsMuteState(true), Times.Once);
        Assert.True(newState);
    }

    [Fact]
    public void HandleMenuEvent_TtsMuteToggleClicked_WhenMuted_TogglesAndFiresEvent()
    {
        _stateManagerMock.Setup(s => s.IsTtsMuted).Returns(true);
        bool? newState = null;
        _router.OnTtsMuteToggleRequested += state => newState = state;

        _router.HandleMenuEvent(MenuItemIds.TtsMuteToggleId, "clicked");

        _stateManagerMock.Verify(s => s.UpdateTtsMuteState(false), Times.Once);
        Assert.False(newState);
    }

    #endregion

    #region HandleMenuEvent - ShowLogs

    [Fact]
    public void HandleMenuEvent_ShowLogsClicked_FiresOnShowLogsRequested()
    {
        var eventFired = false;
        _router.OnShowLogsRequested += () => eventFired = true;

        _router.HandleMenuEvent(MenuItemIds.ShowLogsId, "clicked");

        Assert.True(eventFired);
    }

    #endregion

    #region HandleMenuEvent - LogViewer

    [Fact]
    public void HandleMenuEvent_LogViewerClicked_WhenRunning_FiresOnStopLogViewerRequested()
    {
        _stateManagerMock.Setup(s => s.LogViewerStatus).Returns("Running");
        var stopFired = false;
        var startFired = false;
        _router.OnStopLogViewerRequested += () => stopFired = true;
        _router.OnStartLogViewerRequested += () => startFired = true;

        _router.HandleMenuEvent(MenuItemIds.LogViewerId, "clicked");

        Assert.True(stopFired);
        Assert.False(startFired);
    }

    [Fact]
    public void HandleMenuEvent_LogViewerClicked_WhenStopped_FiresOnStartLogViewerRequested()
    {
        _stateManagerMock.Setup(s => s.LogViewerStatus).Returns("Stopped");
        var stopFired = false;
        var startFired = false;
        _router.OnStopLogViewerRequested += () => stopFired = true;
        _router.OnStartLogViewerRequested += () => startFired = true;

        _router.HandleMenuEvent(MenuItemIds.LogViewerId, "clicked");

        Assert.False(stopFired);
        Assert.True(startFired);
    }

    #endregion

    #region HandleMenuEvent - LlmCorrection

    [Fact]
    public void HandleMenuEvent_LlmCorrectionClicked_WhenDisabled_TogglesAndFiresEvent()
    {
        _stateManagerMock.Setup(s => s.IsLlmCorrectionEnabled).Returns(false);
        bool? newState = null;
        _router.OnLlmCorrectionToggled += state => newState = state;

        _router.HandleMenuEvent(MenuItemIds.LlmCorrectionId, "clicked");

        _stateManagerMock.Verify(s => s.UpdateLlmCorrectionStatus(true), Times.Once);
        Assert.True(newState);
    }

    [Fact]
    public void HandleMenuEvent_LlmCorrectionClicked_WhenEnabled_TogglesAndFiresEvent()
    {
        _stateManagerMock.Setup(s => s.IsLlmCorrectionEnabled).Returns(true);
        bool? newState = null;
        _router.OnLlmCorrectionToggled += state => newState = state;

        _router.HandleMenuEvent(MenuItemIds.LlmCorrectionId, "clicked");

        _stateManagerMock.Verify(s => s.UpdateLlmCorrectionStatus(false), Times.Once);
        Assert.False(newState);
    }

    #endregion

    #region HandleMenuEvent - ReloadPrompt

    [Fact]
    public void HandleMenuEvent_ReloadPromptClicked_FiresOnReloadPromptRequested()
    {
        var eventFired = false;
        _router.OnReloadPromptRequested += () => eventFired = true;

        _router.HandleMenuEvent(MenuItemIds.ReloadPromptId, "clicked");

        Assert.True(eventFired);
    }

    #endregion

    #region HandleMenuEvent - DictationToggle

    [Fact]
    public void HandleMenuEvent_DictationToggleClicked_WhenDisabled_TogglesAndFiresEvent()
    {
        _stateManagerMock.Setup(s => s.IsDictationEnabled).Returns(false);
        bool? newState = null;
        _router.OnDictationToggleRequested += state => newState = state;

        _router.HandleMenuEvent(MenuItemIds.DictationToggleId, "clicked");

        _stateManagerMock.Verify(s => s.UpdateDictationStatus(true), Times.Once);
        Assert.True(newState);
    }

    [Fact]
    public void HandleMenuEvent_DictationToggleClicked_WhenEnabled_TogglesAndFiresEvent()
    {
        _stateManagerMock.Setup(s => s.IsDictationEnabled).Returns(true);
        bool? newState = null;
        _router.OnDictationToggleRequested += state => newState = state;

        _router.HandleMenuEvent(MenuItemIds.DictationToggleId, "clicked");

        _stateManagerMock.Verify(s => s.UpdateDictationStatus(false), Times.Once);
        Assert.False(newState);
    }

    #endregion

    #region HandleMenuEvent - Unknown Menu Item

    [Fact]
    public void HandleMenuEvent_UnknownMenuId_LogsWarning()
    {
        _router.HandleMenuEvent(9999, "clicked");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unknown menu item")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Dictionary-Based Routing Verification

    [Fact]
    public void HandleMenuEvent_AllKnownMenuItems_AreHandled()
    {
        var handledIds = new List<int>();
        
        _router.OnQuitRequested += () => handledIds.Add(MenuItemIds.QuitId);
        _router.OnMuteToggleRequested += () => handledIds.Add(MenuItemIds.MuteToggleId);
        _router.OnTtsMuteToggleRequested += _ => handledIds.Add(MenuItemIds.TtsMuteToggleId);
        _router.OnShowLogsRequested += () => handledIds.Add(MenuItemIds.ShowLogsId);
        _router.OnStopLogViewerRequested += () => handledIds.Add(MenuItemIds.LogViewerId);
        _router.OnLlmCorrectionToggled += _ => handledIds.Add(MenuItemIds.LlmCorrectionId);
        _router.OnReloadPromptRequested += () => handledIds.Add(MenuItemIds.ReloadPromptId);
        _router.OnDictationToggleRequested += _ => handledIds.Add(MenuItemIds.DictationToggleId);

        _stateManagerMock.Setup(s => s.LogViewerStatus).Returns("Running");

        _router.HandleMenuEvent(MenuItemIds.QuitId, "clicked");
        _router.HandleMenuEvent(MenuItemIds.MuteToggleId, "clicked");
        _router.HandleMenuEvent(MenuItemIds.TtsMuteToggleId, "clicked");
        _router.HandleMenuEvent(MenuItemIds.ShowLogsId, "clicked");
        _router.HandleMenuEvent(MenuItemIds.LogViewerId, "clicked");
        _router.HandleMenuEvent(MenuItemIds.LlmCorrectionId, "clicked");
        _router.HandleMenuEvent(MenuItemIds.ReloadPromptId, "clicked");
        _router.HandleMenuEvent(MenuItemIds.DictationToggleId, "clicked");

        Assert.Equal(8, handledIds.Count);
        Assert.Contains(MenuItemIds.QuitId, handledIds);
        Assert.Contains(MenuItemIds.MuteToggleId, handledIds);
        Assert.Contains(MenuItemIds.TtsMuteToggleId, handledIds);
        Assert.Contains(MenuItemIds.ShowLogsId, handledIds);
        Assert.Contains(MenuItemIds.LogViewerId, handledIds);
        Assert.Contains(MenuItemIds.LlmCorrectionId, handledIds);
        Assert.Contains(MenuItemIds.ReloadPromptId, handledIds);
        Assert.Contains(MenuItemIds.DictationToggleId, handledIds);
    }

    #endregion
}
