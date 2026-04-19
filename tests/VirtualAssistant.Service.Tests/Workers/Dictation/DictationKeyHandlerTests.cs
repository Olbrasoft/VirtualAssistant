using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.StateMachine;
using Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Workers.Dictation;

/// <summary>
/// Pins the ScrollLock/Pause routing tree lifted out of DictationWorker in
/// #969. Handler subscribes to IKeyboardMonitor at Start time and fans each
/// release to one of the four IDictationKeyHandlerBindings actions based on
/// the current state machine + the enabled flag.
/// </summary>
public class DictationKeyHandlerTests
{
    private readonly Mock<ILogger<DictationKeyHandler>> _loggerMock = new();
    private readonly Mock<IKeyboardMonitor> _keyboardMonitorMock = new();
    private readonly Mock<IDictationKeyHandlerBindings> _bindingsMock = new();

    private EventHandler<KeyEventArgs>? _capturedHandler;

    public DictationKeyHandlerTests()
    {
        _keyboardMonitorMock.SetupAdd(x => x.KeyReleased += It.IsAny<EventHandler<KeyEventArgs>>())
            .Callback<EventHandler<KeyEventArgs>>(h => _capturedHandler = h);

        // Default: enabled + Idle. Individual tests override.
        _bindingsMock.SetupGet(x => x.IsEnabled).Returns(true);
        _bindingsMock.SetupGet(x => x.State).Returns(DictationState.Idle);
    }

    private DictationKeyHandler CreateSut() =>
        new(_loggerMock.Object, _keyboardMonitorMock.Object);

    [Fact]
    public void Start_SubscribesToKeyReleased()
    {
        var sut = CreateSut();
        sut.Start(_bindingsMock.Object);

        _keyboardMonitorMock.VerifyAdd(x => x.KeyReleased += It.IsAny<EventHandler<KeyEventArgs>>(), Times.Once);
    }

    [Fact]
    public void Start_Twice_SubscribesOnlyOnce()
    {
        var sut = CreateSut();
        sut.Start(_bindingsMock.Object);
        sut.Start(_bindingsMock.Object);

        _keyboardMonitorMock.VerifyAdd(x => x.KeyReleased += It.IsAny<EventHandler<KeyEventArgs>>(), Times.Once);
    }

    [Fact]
    public void Stop_UnsubscribesFromKeyReleased()
    {
        var sut = CreateSut();
        sut.Start(_bindingsMock.Object);

        sut.Stop();

        _keyboardMonitorMock.VerifyRemove(x => x.KeyReleased -= It.IsAny<EventHandler<KeyEventArgs>>(), Times.Once);
    }

    [Fact]
    public void Stop_WithoutStart_DoesNotThrow()
    {
        var sut = CreateSut();
        sut.Stop(); // must be idempotent — DI shutdown may call it unconditionally

        _keyboardMonitorMock.VerifyRemove(x => x.KeyReleased -= It.IsAny<EventHandler<KeyEventArgs>>(), Times.Never);
    }

    [Fact]
    public async Task Disabled_IgnoresAllKeys()
    {
        _bindingsMock.SetupGet(x => x.IsEnabled).Returns(false);
        var sut = CreateSut();
        sut.Start(_bindingsMock.Object);

        _capturedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(50);

        _bindingsMock.Verify(x => x.StartAsync(), Times.Never);
        _bindingsMock.Verify(x => x.CancelRecordingAsync(), Times.Never);
    }

    [Fact]
    public async Task NonScrollLockNonPauseKey_IsIgnored()
    {
        var sut = CreateSut();
        sut.Start(_bindingsMock.Object);

        _capturedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.Escape, IsPressed = false });
        await Task.Delay(50);

        _bindingsMock.Verify(x => x.StartAsync(), Times.Never);
        _bindingsMock.Verify(x => x.StopAndTranscribeAsync(), Times.Never);
    }

    [Fact]
    public async Task ScrollLock_WhileIdle_TriggersStart()
    {
        _bindingsMock.SetupGet(x => x.State).Returns(DictationState.Idle);
        var sut = CreateSut();
        sut.Start(_bindingsMock.Object);

        _capturedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(100);

        _bindingsMock.Verify(x => x.StartAsync(), Times.Once);
    }

    [Fact]
    public async Task ScrollLock_WhileRecording_TriggersStopAndTranscribe()
    {
        _bindingsMock.SetupGet(x => x.State).Returns(DictationState.Recording);
        var sut = CreateSut();
        sut.Start(_bindingsMock.Object);

        _capturedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(100);

        _bindingsMock.Verify(x => x.StopAndTranscribeAsync(), Times.Once);
    }

    [Fact]
    public async Task ScrollLock_WhileTranscribing_IsIgnored()
    {
        // ScrollLock during transcribe is explicitly ignored — Pause is the
        // cancel key to avoid accidental cancel-on-toggle.
        _bindingsMock.SetupGet(x => x.State).Returns(DictationState.Transcribing);
        var sut = CreateSut();
        sut.Start(_bindingsMock.Object);

        _capturedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });
        await Task.Delay(50);

        _bindingsMock.Verify(x => x.StartAsync(), Times.Never);
        _bindingsMock.Verify(x => x.StopAndTranscribeAsync(), Times.Never);
        _bindingsMock.Verify(x => x.CancelTranscription(), Times.Never);
    }

    [Fact]
    public async Task Pause_WhileRecording_CancelsRecording()
    {
        _bindingsMock.SetupGet(x => x.State).Returns(DictationState.Recording);
        _bindingsMock.Setup(x => x.CancelRecordingAsync()).Returns(Task.CompletedTask);
        var sut = CreateSut();
        sut.Start(_bindingsMock.Object);

        _capturedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.Pause, IsPressed = false });
        await Task.Delay(50);

        _bindingsMock.Verify(x => x.CancelRecordingAsync(), Times.Once);
    }

    [Fact]
    public async Task Pause_WhileTranscribing_CancelsTranscription()
    {
        _bindingsMock.SetupGet(x => x.State).Returns(DictationState.Transcribing);
        var sut = CreateSut();
        sut.Start(_bindingsMock.Object);

        _capturedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.Pause, IsPressed = false });
        await Task.Delay(50);

        _bindingsMock.Verify(x => x.CancelTranscription(), Times.Once);
    }

    [Fact]
    public async Task Pause_WhileIdle_IsIgnored()
    {
        _bindingsMock.SetupGet(x => x.State).Returns(DictationState.Idle);
        var sut = CreateSut();
        sut.Start(_bindingsMock.Object);

        _capturedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.Pause, IsPressed = false });
        await Task.Delay(50);

        _bindingsMock.Verify(x => x.CancelRecordingAsync(), Times.Never);
        _bindingsMock.Verify(x => x.CancelTranscription(), Times.Never);
    }

    [Fact]
    public void Constructor_NullLogger_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new DictationKeyHandler(null!, _keyboardMonitorMock.Object));

    [Fact]
    public void Constructor_NullKeyboardMonitor_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new DictationKeyHandler(_loggerMock.Object, null!));

    [Fact]
    public void Start_NullBindings_Throws()
    {
        var sut = CreateSut();
        Assert.Throws<ArgumentNullException>(() => sut.Start(null!));
    }
}
