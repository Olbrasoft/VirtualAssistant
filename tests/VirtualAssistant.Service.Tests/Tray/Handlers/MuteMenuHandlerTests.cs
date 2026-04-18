using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Tray.Handlers;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Tray.Handlers;

public class MuteMenuHandlerTests
{
    private readonly Mock<ILogger<MuteMenuHandler>> _loggerMock = new();
    private readonly Mock<IManualMuteService> _muteServiceMock = new();
    private readonly Mock<ISettingsService> _settingsServiceMock = new();
    private readonly MuteMenuHandler _sut;

    public MuteMenuHandlerTests()
    {
        _sut = new MuteMenuHandler(_loggerMock.Object, _muteServiceMock.Object, _settingsServiceMock.Object);
    }

    [Fact]
    public void HandleMuteToggle_CallsMuteServiceToggle()
    {
        _sut.HandleMuteToggle();
        _muteServiceMock.Verify(x => x.Toggle(), Times.Once);
    }

    [Fact]
    public void HandleMuteToggle_WhenServiceThrows_SwallowsException()
    {
        _muteServiceMock.Setup(x => x.Toggle()).Throws<InvalidOperationException>();

        var ex = Record.Exception(() => _sut.HandleMuteToggle());

        Assert.Null(ex);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleTtsMuteToggleAsync_WritesSettingWithGivenValue(bool muted)
    {
        await _sut.HandleTtsMuteToggleAsync(muted);

        _settingsServiceMock.Verify(x => x.SetAsync("tts.muted", muted), Times.Once);
    }

    [Fact]
    public async Task HandleTtsMuteToggleAsync_WhenSettingsThrows_SwallowsException()
    {
        _settingsServiceMock
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ThrowsAsync(new InvalidOperationException());

        var ex = await Record.ExceptionAsync(() => _sut.HandleTtsMuteToggleAsync(true));

        Assert.Null(ex);
    }
}
