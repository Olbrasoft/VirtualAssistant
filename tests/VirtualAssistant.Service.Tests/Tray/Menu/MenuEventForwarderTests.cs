using Moq;
using Olbrasoft.VirtualAssistant.Service.Tray.Menu;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Tray.Menu;

/// <summary>
/// Unit tests for <see cref="MenuEventForwarder"/>. This class is pure event
/// wiring — it subscribes to <see cref="IMenuEventRouter"/> and re-raises
/// each event — so the tests pin that subscription happens in the ctor and
/// events re-emit to the forwarder's own subscribers, and that Dispose
/// unsubscribes from the router. A regression here would drop menu clicks
/// silently at runtime.
/// </summary>
public class MenuEventForwarderTests
{
    private readonly Mock<IMenuEventRouter> _routerMock = new();

    [Fact]
    public void Constructor_NullRouter_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new MenuEventForwarder(null!));

    [Fact]
    public void Constructor_SubscribesToAllRouterEvents()
    {
        var sut = new MenuEventForwarder(_routerMock.Object);

        _routerMock.VerifyAdd(x => x.OnQuitRequested += It.IsAny<Action>(), Times.Once);
        _routerMock.VerifyAdd(x => x.OnMuteToggleRequested += It.IsAny<Action>(), Times.Once);
        _routerMock.VerifyAdd(x => x.OnDashboardRequested += It.IsAny<Action>(), Times.Once);
        _routerMock.VerifyAdd(x => x.OnAboutRequested += It.IsAny<Action>(), Times.Once);
        _routerMock.VerifyAdd(x => x.OnLlmCorrectionToggled += It.IsAny<Action<bool>>(), Times.Once);
        _routerMock.VerifyAdd(x => x.OnReloadPromptRequested += It.IsAny<Action>(), Times.Once);
        _routerMock.VerifyAdd(x => x.OnDictationToggleRequested += It.IsAny<Action<bool>>(), Times.Once);
        _routerMock.VerifyAdd(x => x.OnTtsMuteToggleRequested += It.IsAny<Action<bool>>(), Times.Once);
        _routerMock.VerifyAdd(x => x.OnMercuryBillingRequested += It.IsAny<Action>(), Times.Once);
        _routerMock.VerifyAdd(x => x.OnStreamingTranscriptionToggled += It.IsAny<Action<bool>>(), Times.Once);
        _routerMock.VerifyAdd(x => x.OnReloadCorrectionsCacheRequested += It.IsAny<Action>(), Times.Once);
        GC.KeepAlive(sut);
    }

    [Fact]
    public void RouterQuitEvent_ReRaisedOnForwarder()
    {
        var sut = new MenuEventForwarder(_routerMock.Object);
        var raised = 0;
        sut.OnQuitRequested += () => raised++;

        _routerMock.Raise(x => x.OnQuitRequested += null);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void RouterLlmCorrectionToggled_ReRaisedWithArgument()
    {
        var sut = new MenuEventForwarder(_routerMock.Object);
        bool? received = null;
        sut.OnLlmCorrectionToggled += enabled => received = enabled;

        _routerMock.Raise(x => x.OnLlmCorrectionToggled += null, true);

        Assert.Equal(true, received);
    }

    [Fact]
    public void RouterDictationToggled_ReRaisedWithArgument()
    {
        var sut = new MenuEventForwarder(_routerMock.Object);
        bool? received = null;
        sut.OnDictationToggleRequested += enabled => received = enabled;

        _routerMock.Raise(x => x.OnDictationToggleRequested += null, false);

        Assert.Equal(false, received);
    }

    [Fact]
    public void Dispose_UnsubscribesFromAllRouterEvents()
    {
        var sut = new MenuEventForwarder(_routerMock.Object);

        sut.Dispose();

        _routerMock.VerifyRemove(x => x.OnQuitRequested -= It.IsAny<Action>(), Times.Once);
        _routerMock.VerifyRemove(x => x.OnMuteToggleRequested -= It.IsAny<Action>(), Times.Once);
        _routerMock.VerifyRemove(x => x.OnDashboardRequested -= It.IsAny<Action>(), Times.Once);
        _routerMock.VerifyRemove(x => x.OnAboutRequested -= It.IsAny<Action>(), Times.Once);
        _routerMock.VerifyRemove(x => x.OnLlmCorrectionToggled -= It.IsAny<Action<bool>>(), Times.Once);
        _routerMock.VerifyRemove(x => x.OnReloadPromptRequested -= It.IsAny<Action>(), Times.Once);
        _routerMock.VerifyRemove(x => x.OnDictationToggleRequested -= It.IsAny<Action<bool>>(), Times.Once);
        _routerMock.VerifyRemove(x => x.OnTtsMuteToggleRequested -= It.IsAny<Action<bool>>(), Times.Once);
        _routerMock.VerifyRemove(x => x.OnMercuryBillingRequested -= It.IsAny<Action>(), Times.Once);
        _routerMock.VerifyRemove(x => x.OnStreamingTranscriptionToggled -= It.IsAny<Action<bool>>(), Times.Once);
        _routerMock.VerifyRemove(x => x.OnReloadCorrectionsCacheRequested -= It.IsAny<Action>(), Times.Once);
    }
}
