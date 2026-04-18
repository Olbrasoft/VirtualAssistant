using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Tray.Handlers;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Tray.Handlers;

public class DictationMenuHandlerTests
{
    private readonly Mock<ILogger<DictationMenuHandler>> _loggerMock = new();

    [Fact]
    public void HandleDictationToggle_WithoutDictationControl_IsNoOp()
    {
        // Running without dictation support is a valid deployment, so a missing
        // control service must not crash the menu click.
        var sut = new DictationMenuHandler(_loggerMock.Object, dictationControl: null);

        var ex = Record.Exception(() => sut.HandleDictationToggle(true));

        Assert.Null(ex);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HandleDictationToggle_DelegatesValueToControl(bool enabled)
    {
        var controlMock = new Mock<IDictationControl>();
        var sut = new DictationMenuHandler(_loggerMock.Object, controlMock.Object);

        sut.HandleDictationToggle(enabled);

        controlMock.Verify(x => x.SetDictationEnabled(enabled), Times.Once);
    }

    [Fact]
    public void HandleDictationToggle_WhenControlThrows_SwallowsException()
    {
        var controlMock = new Mock<IDictationControl>();
        controlMock.Setup(x => x.SetDictationEnabled(It.IsAny<bool>())).Throws<InvalidOperationException>();
        var sut = new DictationMenuHandler(_loggerMock.Object, controlMock.Object);

        var ex = Record.Exception(() => sut.HandleDictationToggle(true));

        Assert.Null(ex);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HandleStreamingTranscriptionToggle_DelegatesValueToControl(bool enabled)
    {
        var controlMock = new Mock<IDictationControl>();
        var sut = new DictationMenuHandler(_loggerMock.Object, controlMock.Object);

        sut.HandleStreamingTranscriptionToggle(enabled);

        controlMock.Verify(x => x.SetStreamingTranscriptionEnabled(enabled), Times.Once);
    }

    [Fact]
    public void HandleStreamingTranscriptionToggle_WithoutDictationControl_IsNoOp()
    {
        var sut = new DictationMenuHandler(_loggerMock.Object, dictationControl: null);

        var ex = Record.Exception(() => sut.HandleStreamingTranscriptionToggle(true));

        Assert.Null(ex);
    }
}
