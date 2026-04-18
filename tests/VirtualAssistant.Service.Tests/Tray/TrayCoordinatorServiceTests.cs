using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Tray;
using Olbrasoft.VirtualAssistant.Service.Tray.Menu;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Tray;

/// <summary>
/// Unit tests for <see cref="TrayCoordinatorService"/>. After the #1007
/// cleanup the coordinator only keeps the dependencies it actually uses
/// (icon coordinator, menu dispatcher, menu event forwarder, state
/// notification handler).
/// </summary>
public class TrayCoordinatorServiceTests
{
    private readonly Mock<ILogger<TrayCoordinatorService>> _loggerMock = new();
    private readonly Mock<ITrayIconCoordinator> _iconCoordinatorMock = new();
    private readonly Mock<IMenuEventDispatcher> _menuDispatcherMock = new();
    private readonly Mock<IMenuEventForwarder> _menuEventForwarderMock = new();
    private readonly Mock<IStateNotificationHandler> _stateHandlerMock = new();

    [Fact]
    public void Constructor_WithNullLogger_Throws() =>
        Assert.Equal("logger", Assert.Throws<ArgumentNullException>(() =>
            new TrayCoordinatorService(null!, _iconCoordinatorMock.Object, _menuDispatcherMock.Object,
                _menuEventForwarderMock.Object, _stateHandlerMock.Object)).ParamName);

    [Fact]
    public void Constructor_WithNullIconCoordinator_Throws() =>
        Assert.Equal("iconCoordinator", Assert.Throws<ArgumentNullException>(() =>
            new TrayCoordinatorService(_loggerMock.Object, null!, _menuDispatcherMock.Object,
                _menuEventForwarderMock.Object, _stateHandlerMock.Object)).ParamName);

    [Fact]
    public void Constructor_WithNullMenuDispatcher_Throws() =>
        Assert.Equal("menuDispatcher", Assert.Throws<ArgumentNullException>(() =>
            new TrayCoordinatorService(_loggerMock.Object, _iconCoordinatorMock.Object, null!,
                _menuEventForwarderMock.Object, _stateHandlerMock.Object)).ParamName);

    [Fact]
    public void Constructor_WithNullMenuEventForwarder_Throws() =>
        Assert.Equal("menuEventForwarder", Assert.Throws<ArgumentNullException>(() =>
            new TrayCoordinatorService(_loggerMock.Object, _iconCoordinatorMock.Object, _menuDispatcherMock.Object,
                null!, _stateHandlerMock.Object)).ParamName);

    [Fact]
    public void Constructor_WithNullStateHandler_Throws() =>
        Assert.Equal("stateHandler", Assert.Throws<ArgumentNullException>(() =>
            new TrayCoordinatorService(_loggerMock.Object, _iconCoordinatorMock.Object, _menuDispatcherMock.Object,
                _menuEventForwarderMock.Object, null!)).ParamName);

    [Fact]
    public async Task InitializeAsync_CallsIconCoordinatorInitialize()
    {
        var coordinator = CreateCoordinator();
        await coordinator.InitializeAsync();
        _iconCoordinatorMock.Verify(x => x.InitializeIconsAsync(), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_CallsStateHandlerSubscribe()
    {
        var coordinator = CreateCoordinator();
        await coordinator.InitializeAsync();
        _stateHandlerMock.Verify(x => x.SubscribeToEvents(), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_CallsStateHandlerInitialize()
    {
        var coordinator = CreateCoordinator();
        await coordinator.InitializeAsync();
        _stateHandlerMock.Verify(x => x.InitializeStatesAsync(), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_WhenIconCoordinatorThrows_PropagatesException()
    {
        _iconCoordinatorMock.Setup(x => x.InitializeIconsAsync())
            .ThrowsAsync(new InvalidOperationException("Icon initialization failed"));
        var coordinator = CreateCoordinator();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.InitializeAsync());

        Assert.Equal("Icon initialization failed", thrown.Message);
    }

    [Fact]
    public void Dispose_UnsubscribesFromMuteToggleEvent()
    {
        // Named for what it asserts — Dispose detaches the coordinator's
        // MuteToggle handler from the forwarder. (Copilot on #1007 flagged
        // the previous name as misleading.)
        var coordinator = CreateCoordinator();
        coordinator.Dispose();

        _menuEventForwarderMock.VerifyRemove(
            x => x.OnMuteToggleRequested -= It.IsAny<Action>(), Times.Once);
    }

    [Fact]
    public void Dispose_UnsubscribesFromStateHandler()
    {
        var coordinator = CreateCoordinator();
        coordinator.Dispose();
        _stateHandlerMock.Verify(x => x.UnsubscribeFromEvents(), Times.Once);
    }

    [Fact]
    public void Dispose_UnsubscribesFromMenuEventForwarder()
    {
        var coordinator = CreateCoordinator();
        coordinator.Dispose();

        _menuEventForwarderMock.VerifyRemove(x => x.OnQuitRequested -= It.IsAny<Action>(), Times.Once);
        _menuEventForwarderMock.VerifyRemove(x => x.OnReloadPromptRequested -= It.IsAny<Action>(), Times.Once);
        _menuEventForwarderMock.VerifyRemove(x => x.OnReloadCorrectionsCacheRequested -= It.IsAny<Action>(), Times.Once);
    }

    [Fact]
    public void Dispose_MultipleTimes_OnlyUnsubscribesOnce()
    {
        var coordinator = CreateCoordinator();
        coordinator.Dispose();
        coordinator.Dispose();
        _stateHandlerMock.Verify(x => x.UnsubscribeFromEvents(), Times.Once);
    }

    private TrayCoordinatorService CreateCoordinator() => new(
        _loggerMock.Object,
        _iconCoordinatorMock.Object,
        _menuDispatcherMock.Object,
        _menuEventForwarderMock.Object,
        _stateHandlerMock.Object);
}
