using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Services;

namespace VirtualAssistant.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="TrayIconCoordinator"/>.
/// Verifies correct tray icon management for 3 icons (left hand, center, right hand).
/// </summary>
public class TrayIconCoordinatorTests
{
    private readonly Mock<ITrayIconManager> _managerMock;
    private readonly Mock<IManualMuteService> _muteServiceMock;
    private readonly Mock<ILogger<TrayIconCoordinator>> _loggerMock;
    private readonly Mock<Olbrasoft.VirtualAssistant.Core.Services.ITrayMenuHandler> _menuHandlerMock;
    private readonly Mock<Olbrasoft.VirtualAssistant.Core.Services.ITrayIcon> _trayIconMock;
    private readonly string _iconsPath = "/test/icons";

    public TrayIconCoordinatorTests()
    {
        _managerMock = new Mock<ITrayIconManager>();
        _muteServiceMock = new Mock<IManualMuteService>();
        _loggerMock = new Mock<ILogger<TrayIconCoordinator>>();
        _menuHandlerMock = new Mock<Olbrasoft.VirtualAssistant.Core.Services.ITrayMenuHandler>();
        _trayIconMock = new Mock<Olbrasoft.VirtualAssistant.Core.Services.ITrayIcon>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullManager_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new TrayIconCoordinator(null!, _iconsPath, _muteServiceMock.Object, _loggerMock.Object));
        Assert.Equal("manager", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullIconsPath_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new TrayIconCoordinator(_managerMock.Object, null!, _muteServiceMock.Object, _loggerMock.Object));
        Assert.Equal("iconsPath", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullMuteService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new TrayIconCoordinator(_managerMock.Object, _iconsPath, null!, _loggerMock.Object));
        Assert.Equal("muteService", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new TrayIconCoordinator(_managerMock.Object, _iconsPath, _muteServiceMock.Object, null!));
        Assert.Equal("logger", exception.ParamName);
    }

    #endregion

    #region InitializeIconsAsync Tests

    [Fact]
    public async Task InitializeIconsAsync_CreatesAllThreeIcons()
    {
        // Arrange
        _muteServiceMock.Setup(x => x.IsMuted).Returns(false);
        _managerMock.Setup(x => x.CreateIconAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Olbrasoft.VirtualAssistant.Core.Services.ITrayMenuHandler?>()))
            .ReturnsAsync(_trayIconMock.Object);

        var coordinator = new TrayIconCoordinator(
            _managerMock.Object,
            _iconsPath,
            _muteServiceMock.Object,
            _loggerMock.Object,
            _menuHandlerMock.Object);

        // Act
        await coordinator.InitializeIconsAsync();

        // Assert
        _managerMock.Verify(x => x.CreateIconAsync(
            "virtual-assistant-left-hand",
            It.Is<string>(p => p.Contains("hands") && p.Contains("default-left-hand.svg")),
            "VirtualAssistant - Left Hand",
            null), Times.Once);

        _managerMock.Verify(x => x.CreateIconAsync(
            "virtual-assistant-service",
            It.Is<string>(p => p.Contains("virtual-assistant-listening.svg")),
            "VirtualAssistant - poslouchám",
            _menuHandlerMock.Object), Times.Once);

        _managerMock.Verify(x => x.CreateIconAsync(
            "virtual-assistant-right-hand",
            It.Is<string>(p => p.Contains("hands") && p.Contains("default-right-hand.svg")),
            "VirtualAssistant - Right Hand",
            null), Times.Once);
    }

    [Fact]
    public async Task InitializeIconsAsync_WhenMuted_CreatesMutedCenterIcon()
    {
        // Arrange
        _muteServiceMock.Setup(x => x.IsMuted).Returns(true);
        _managerMock.Setup(x => x.CreateIconAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Olbrasoft.VirtualAssistant.Core.Services.ITrayMenuHandler?>()))
            .ReturnsAsync(_trayIconMock.Object);

        var coordinator = new TrayIconCoordinator(
            _managerMock.Object,
            _iconsPath,
            _muteServiceMock.Object,
            _loggerMock.Object);

        // Act
        await coordinator.InitializeIconsAsync();

        // Assert
        _managerMock.Verify(x => x.CreateIconAsync(
            "virtual-assistant-service",
            It.Is<string>(p => p.Contains("virtual-assistant-muted.svg")),
            It.IsAny<string>(),
            null), Times.Once);
    }

    [Fact]
    public async Task InitializeIconsAsync_WhenManagerThrows_PropagatesException()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Icon creation failed");
        _managerMock.Setup(x => x.CreateIconAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Olbrasoft.VirtualAssistant.Core.Services.ITrayMenuHandler?>()))
            .ThrowsAsync(expectedException);

        var coordinator = new TrayIconCoordinator(
            _managerMock.Object,
            _iconsPath,
            _muteServiceMock.Object,
            _loggerMock.Object);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.InitializeIconsAsync());
        Assert.Equal(expectedException, exception);
    }

    #endregion

    #region UpdateCenterIcon Tests

    [Fact]
    public async Task UpdateCenterIcon_WithMutedTrue_SetsMutedIcon()
    {
        // Arrange
        _muteServiceMock.Setup(x => x.IsMuted).Returns(false);
        _managerMock.Setup(x => x.CreateIconAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Olbrasoft.VirtualAssistant.Core.Services.ITrayMenuHandler?>()))
            .ReturnsAsync(_trayIconMock.Object);

        var coordinator = new TrayIconCoordinator(
            _managerMock.Object,
            _iconsPath,
            _muteServiceMock.Object,
            _loggerMock.Object);

        await coordinator.InitializeIconsAsync();

        // Act
        coordinator.UpdateCenterIcon(true);

        // Assert
        _trayIconMock.Verify(x => x.SetIcon(
            It.Is<string>(p => p.Contains("virtual-assistant-muted.svg")),
            "VirtualAssistant - poslouchám"), Times.Once);
    }

    [Fact]
    public async Task UpdateCenterIcon_WithMutedFalse_SetsListeningIcon()
    {
        // Arrange
        _muteServiceMock.Setup(x => x.IsMuted).Returns(true);
        _managerMock.Setup(x => x.CreateIconAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Olbrasoft.VirtualAssistant.Core.Services.ITrayMenuHandler?>()))
            .ReturnsAsync(_trayIconMock.Object);

        var coordinator = new TrayIconCoordinator(
            _managerMock.Object,
            _iconsPath,
            _muteServiceMock.Object,
            _loggerMock.Object);

        await coordinator.InitializeIconsAsync();

        // Act
        coordinator.UpdateCenterIcon(false);

        // Assert
        _trayIconMock.Verify(x => x.SetIcon(
            It.Is<string>(p => p.Contains("virtual-assistant-listening.svg")),
            "VirtualAssistant - poslouchám"), Times.Once);
    }

    [Fact]
    public void UpdateCenterIcon_WhenNotInitialized_LogsWarning()
    {
        // Arrange
        var coordinator = new TrayIconCoordinator(
            _managerMock.Object,
            _iconsPath,
            _muteServiceMock.Object,
            _loggerMock.Object);

        // Act
        coordinator.UpdateCenterIcon(true);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Center icon not initialized")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region SetLeftHandIcon Tests

    [Fact]
    public async Task SetLeftHandIcon_SetsCorrectIcon()
    {
        // Arrange
        _muteServiceMock.Setup(x => x.IsMuted).Returns(false);
        _managerMock.Setup(x => x.CreateIconAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Olbrasoft.VirtualAssistant.Core.Services.ITrayMenuHandler?>()))
            .ReturnsAsync(_trayIconMock.Object);

        var coordinator = new TrayIconCoordinator(
            _managerMock.Object,
            _iconsPath,
            _muteServiceMock.Object,
            _loggerMock.Object);

        await coordinator.InitializeIconsAsync();

        // Act
        coordinator.SetLeftHandIcon("fist-left-hand.svg");

        // Assert
        _trayIconMock.Verify(x => x.SetIcon(
            It.Is<string>(p => p.Contains("hands") && p.Contains("fist-left-hand.svg")),
            "VirtualAssistant - Left Hand"), Times.Once);
    }

    [Fact]
    public void SetLeftHandIcon_WhenNotInitialized_LogsWarning()
    {
        // Arrange
        var coordinator = new TrayIconCoordinator(
            _managerMock.Object,
            _iconsPath,
            _muteServiceMock.Object,
            _loggerMock.Object);

        // Act
        coordinator.SetLeftHandIcon("fist-left-hand.svg");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Left hand icon not initialized")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region SetRightHandIcon Tests

    [Fact]
    public async Task SetRightHandIcon_SetsCorrectIcon()
    {
        // Arrange
        _muteServiceMock.Setup(x => x.IsMuted).Returns(false);
        _managerMock.Setup(x => x.CreateIconAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Olbrasoft.VirtualAssistant.Core.Services.ITrayMenuHandler?>()))
            .ReturnsAsync(_trayIconMock.Object);

        var coordinator = new TrayIconCoordinator(
            _managerMock.Object,
            _iconsPath,
            _muteServiceMock.Object,
            _loggerMock.Object);

        await coordinator.InitializeIconsAsync();

        // Act
        coordinator.SetRightHandIcon("writing-right-hand.svg");

        // Assert
        _trayIconMock.Verify(x => x.SetIcon(
            It.Is<string>(p => p.Contains("hands") && p.Contains("writing-right-hand.svg")),
            "VirtualAssistant - Right Hand"), Times.Once);
    }

    [Fact]
    public void SetRightHandIcon_WhenNotInitialized_LogsWarning()
    {
        // Arrange
        var coordinator = new TrayIconCoordinator(
            _managerMock.Object,
            _iconsPath,
            _muteServiceMock.Object,
            _loggerMock.Object);

        // Act
        coordinator.SetRightHandIcon("writing-right-hand.svg");

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Right hand icon not initialized")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public async Task Dispose_RemovesAllThreeIcons()
    {
        // Arrange
        _muteServiceMock.Setup(x => x.IsMuted).Returns(false);
        _managerMock.Setup(x => x.CreateIconAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Olbrasoft.VirtualAssistant.Core.Services.ITrayMenuHandler?>()))
            .ReturnsAsync(_trayIconMock.Object);

        var coordinator = new TrayIconCoordinator(
            _managerMock.Object,
            _iconsPath,
            _muteServiceMock.Object,
            _loggerMock.Object);

        await coordinator.InitializeIconsAsync();

        // Act
        coordinator.Dispose();

        // Assert
        _managerMock.Verify(x => x.RemoveIcon("virtual-assistant-left-hand"), Times.Once);
        _managerMock.Verify(x => x.RemoveIcon("virtual-assistant-right-hand"), Times.Once);
        _managerMock.Verify(x => x.RemoveIcon("virtual-assistant-service"), Times.Once);
    }

    [Fact]
    public void Dispose_WhenCalledTwice_RemovesIconsOnlyOnce()
    {
        // Arrange
        var coordinator = new TrayIconCoordinator(
            _managerMock.Object,
            _iconsPath,
            _muteServiceMock.Object,
            _loggerMock.Object);

        // Act
        coordinator.Dispose();
        coordinator.Dispose();

        // Assert
        _managerMock.Verify(x => x.RemoveIcon(It.IsAny<string>()), Times.Never);
    }

    #endregion
}
