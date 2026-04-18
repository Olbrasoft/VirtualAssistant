using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Audio;
using Olbrasoft.VirtualAssistant.Core.Keyboard;
using Olbrasoft.VirtualAssistant.Service.Hubs;
using Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Workers.Dictation;

/// <summary>
/// Pins the façade contract lifted out of DictationWorker in #969 — each
/// method forwards to the right dependency, and the broadcast path swallows
/// SignalR transport errors so a client-side failure cannot take down the
/// dictation pipeline.
/// </summary>
public class DictationOutputChannelTests
{
    private readonly Mock<ILogger<DictationOutputChannel>> _loggerMock = new();
    private readonly Mock<IKeyboardSimulationService> _keyboardMock = new();
    private readonly Mock<ISoundEffectPlayer> _typingMock = new();
    private readonly Mock<ISoundEffectPlayer> _cancelMock = new();
    private readonly Mock<IHubContext<DictationHub>> _hubMock = new();
    private readonly Mock<IHubClients> _clientsMock = new();
    private readonly Mock<IClientProxy> _allClientsMock = new();

    private DictationOutputChannel CreateSut()
    {
        _hubMock.SetupGet(x => x.Clients).Returns(_clientsMock.Object);
        _clientsMock.SetupGet(x => x.All).Returns(_allClientsMock.Object);
        return new DictationOutputChannel(
            _loggerMock.Object, _keyboardMock.Object, _typingMock.Object, _cancelMock.Object, _hubMock.Object);
    }

    [Fact]
    public void StartTypingFeedback_CallsTypingPlayerStartLoop()
    {
        var sut = CreateSut();
        sut.StartTypingFeedback();
        _typingMock.Verify(x => x.StartLoop(), Times.Once);
    }

    [Fact]
    public void StopTypingFeedback_CallsTypingPlayerStopLoop()
    {
        var sut = CreateSut();
        sut.StopTypingFeedback();
        _typingMock.Verify(x => x.StopLoop(), Times.Once);
    }

    [Fact]
    public void PlayCancelCue_CallsCancelPlayerPlay()
    {
        var sut = CreateSut();
        sut.PlayCancelCue();
        _cancelMock.Verify(x => x.Play(), Times.Once);
    }

    [Fact]
    public async Task FastPasteAsync_ForwardsToKeyboardService()
    {
        _keyboardMock
            .Setup(x => x.FastPasteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = CreateSut();
        var result = await sut.FastPasteAsync("hello", CancellationToken.None);

        Assert.True(result);
        _keyboardMock.Verify(x => x.FastPasteAsync("hello", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TypeIntoActiveWindowAsync_ForwardsToKeyboardService()
    {
        _keyboardMock
            .Setup(x => x.TypeIntoActiveWindowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = CreateSut();
        var result = await sut.TypeIntoActiveWindowAsync("text", CancellationToken.None);

        Assert.False(result);
        _keyboardMock.Verify(x => x.TypeIntoActiveWindowAsync("text", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BroadcastEventAsync_SendsDictationEventToAllClients()
    {
        object?[]? capturedArgs = null;
        _allClientsMock
            .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, object?[], CancellationToken>((_, args, _) => capturedArgs = args)
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        await sut.BroadcastEventAsync(DictationEventType.RecordingStarted, "hello");

        Assert.NotNull(capturedArgs);
        Assert.Single(capturedArgs!);
        var evt = Assert.IsType<DictationEvent>(capturedArgs![0]);
        Assert.Equal(DictationEventType.RecordingStarted, evt.EventType);
        Assert.Equal("hello", evt.Text);
    }

    [Fact]
    public async Task BroadcastEventAsync_SwallowsHubTransportException()
    {
        // A faulted hub send must not bubble out — the dictation pipeline is
        // allowed to keep running even if every SignalR client is gone. The
        // channel catches the exception at LogDebug level.
        _allClientsMock
            .Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("clients gone"));

        var sut = CreateSut();

        var ex = await Record.ExceptionAsync(() =>
            sut.BroadcastEventAsync(DictationEventType.TranscriptionCompleted, null));

        Assert.Null(ex);
    }
}
