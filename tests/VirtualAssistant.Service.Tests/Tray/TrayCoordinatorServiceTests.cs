using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Tray;

namespace VirtualAssistant.Service.Tests.Tray;

/// <summary>
/// Unit tests for <see cref="TrayCoordinatorService"/>.
/// Verifies orchestration of 5 specialized tray services.
/// </summary>
public class TrayCoordinatorServiceTests
{
    private readonly Mock<ILogger<TrayCoordinatorService>> _loggerMock;
    private readonly Mock<ITrayIconCoordinator> _iconCoordinatorMock;
    private readonly Mock<IMenuEventDispatcher> _menuDispatcherMock;
    private readonly Mock<IServiceLifecycleManager> _lifecycleManagerMock;
    private readonly Mock<IStateNotificationHandler> _stateHandlerMock;
    private readonly Mock<IIconAnimationService> _iconAnimationServiceMock;

    public TrayCoordinatorServiceTests()
    {
        _loggerMock = new Mock<ILogger<TrayCoordinatorService>>();
        _iconCoordinatorMock = new Mock<ITrayIconCoordinator>();
        _menuDispatcherMock = new Mock<IMenuEventDispatcher>();
        _lifecycleManagerMock = new Mock<IServiceLifecycleManager>();
        _stateHandlerMock = new Mock<IStateNotificationHandler>();
        _iconAnimationServiceMock = new Mock<IIconAnimationService>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new TrayCoordinatorService(
                null!,
                _iconCoordinatorMock.Object,
                _menuDispatcherMock.Object,
                _lifecycleManagerMock.Object,
                _stateHandlerMock.Object,
                _iconAnimationServiceMock.Object));
        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullIconCoordinator_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new TrayCoordinatorService(
                _loggerMock.Object,
                null!,
                _menuDispatcherMock.Object,
                _lifecycleManagerMock.Object,
                _stateHandlerMock.Object,
                _iconAnimationServiceMock.Object));
        Assert.Equal("iconCoordinator", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullMenuDispatcher_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new TrayCoordinatorService(
                _loggerMock.Object,
                _iconCoordinatorMock.Object,
                null!,
                _lifecycleManagerMock.Object,
                _stateHandlerMock.Object,
                _iconAnimationServiceMock.Object));
        Assert.Equal("menuDispatcher", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullLifecycleManager_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new TrayCoordinatorService(
                _loggerMock.Object,
                _iconCoordinatorMock.Object,
                _menuDispatcherMock.Object,
                null!,
                _stateHandlerMock.Object,
                _iconAnimationServiceMock.Object));
        Assert.Equal("lifecycleManager", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullStateHandler_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new TrayCoordinatorService(
                _loggerMock.Object,
                _iconCoordinatorMock.Object,
                _menuDispatcherMock.Object,
                _lifecycleManagerMock.Object,
                null!,
                _iconAnimationServiceMock.Object));
        Assert.Equal("stateHandler", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullIconAnimationService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new TrayCoordinatorService(
                _loggerMock.Object,
                _iconCoordinatorMock.Object,
                _menuDispatcherMock.Object,
                _lifecycleManagerMock.Object,
                _stateHandlerMock.Object,
                null!));
        Assert.Equal("iconAnimationService", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullMenuHandler_DoesNotThrow()
    {
        // Act & Assert - should not throw
        var coordinator = new TrayCoordinatorService(
            _loggerMock.Object,
            _iconCoordinatorMock.Object,
            _menuDispatcherMock.Object,
            _lifecycleManagerMock.Object,
            _stateHandlerMock.Object,
            _iconAnimationServiceMock.Object,
            null);
        Assert.NotNull(coordinator);
    }

    #endregion

    #region InitializeAsync Tests

    [Fact]
    public async Task InitializeAsync_CallsIconCoordinatorInitialize()
    {
        // Arrange
        var coordinator = CreateCoordinator();

        // Act
        await coordinator.InitializeAsync();

        // Assert
        _iconCoordinatorMock.Verify(x => x.InitializeIconsAsync(), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_CallsStateHandlerSubscribe()
    {
        // Arrange
        var coordinator = CreateCoordinator();

        // Act
        await coordinator.InitializeAsync();

        // Assert
        _stateHandlerMock.Verify(x => x.SubscribeToEvents(), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_CallsStateHandlerInitialize()
    {
        // Arrange
        var coordinator = CreateCoordinator();

        // Act
        await coordinator.InitializeAsync();

        // Assert
        _stateHandlerMock.Verify(x => x.InitializeStatesAsync(), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_WhenIconCoordinatorThrows_PropagatesException()
    {
        // Arrange
        var exception = new InvalidOperationException("Icon initialization failed");
        _iconCoordinatorMock.Setup(x => x.InitializeIconsAsync()).ThrowsAsync(exception);
        var coordinator = CreateCoordinator();

        // Act & Assert
        var thrownException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.InitializeAsync());
        Assert.Equal("Icon initialization failed", thrownException.Message);
    }

    #endregion

    // Note: Event wiring tests removed - VirtualAssistantDBusMenuHandler is internal
    // Event wiring is tested via integration tests

    #region Dispose Tests

    [Fact]
    public void Dispose_CallsStateHandlerUnsubscribe()
    {
        // Arrange
        var coordinator = CreateCoordinator();

        // Act
        coordinator.Dispose();

        // Assert
        _stateHandlerMock.Verify(x => x.UnsubscribeFromEvents(), Times.Once);
    }

    [Fact]
    public void Dispose_MultipleTimes_OnlyUnsubscribesOnce()
    {
        // Arrange
        var coordinator = CreateCoordinator();

        // Act
        coordinator.Dispose();
        coordinator.Dispose();

        // Assert
        _stateHandlerMock.Verify(x => x.UnsubscribeFromEvents(), Times.Once);
    }

    #endregion

    private TrayCoordinatorService CreateCoordinator()
    {
        return new TrayCoordinatorService(
            _loggerMock.Object,
            _iconCoordinatorMock.Object,
            _menuDispatcherMock.Object,
            _lifecycleManagerMock.Object,
            _stateHandlerMock.Object,
            _iconAnimationServiceMock.Object);
    }
}
