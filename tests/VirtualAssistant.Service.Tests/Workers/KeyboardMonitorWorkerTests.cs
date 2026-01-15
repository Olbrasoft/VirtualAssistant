using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Service.Workers;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Workers;

public class KeyboardMonitorWorkerTests : IDisposable
{
    private readonly Mock<ILogger<KeyboardMonitorWorker>> _loggerMock;
    private readonly Mock<IKeyboardMonitor> _keyboardMonitorMock;
    private readonly Mock<IManualMuteService> _muteServiceMock;
    private readonly KeyboardMonitorWorker _sut;

    private EventHandler<KeyEventArgs>? _capturedKeyReleasedHandler;

    public KeyboardMonitorWorkerTests()
    {
        _loggerMock = new Mock<ILogger<KeyboardMonitorWorker>>();
        _keyboardMonitorMock = new Mock<IKeyboardMonitor>();
        _muteServiceMock = new Mock<IManualMuteService>();

        _keyboardMonitorMock.SetupAdd(x => x.KeyReleased += It.IsAny<EventHandler<KeyEventArgs>>())
            .Callback<EventHandler<KeyEventArgs>>(handler => _capturedKeyReleasedHandler = handler);

        _sut = new KeyboardMonitorWorker(
            _loggerMock.Object,
            _keyboardMonitorMock.Object,
            _muteServiceMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_SubscribesToKeyReleasedEvent()
    {
        _keyboardMonitorMock.VerifyAdd(x => x.KeyReleased += It.IsAny<EventHandler<KeyEventArgs>>(), Times.Once);
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_StartsKeyboardMonitor()
    {
        using var cts = new CancellationTokenSource();
        _keyboardMonitorMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _ = _sut.StartAsync(cts.Token);
        await Task.Delay(100);
        await cts.CancelAsync();

        _keyboardMonitorMock.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region StopAsync Tests

    [Fact]
    public async Task StopAsync_StopsKeyboardMonitorAndUnsubscribes()
    {
        using var cts = new CancellationTokenSource();
        _keyboardMonitorMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.StartAsync(cts.Token);
        await Task.Delay(50);
        await _sut.StopAsync(CancellationToken.None);

        _keyboardMonitorMock.Verify(x => x.Stop(), Times.Once);
        _keyboardMonitorMock.VerifyRemove(x => x.KeyReleased -= It.IsAny<EventHandler<KeyEventArgs>>(), Times.Once);
    }

    #endregion

    #region Key Event Handling Tests

    [Fact]
    public void OnKeyReleased_ScrollLock_DoesNotToggleMute()
    {
        // ScrollLock is now used for dictation (DictationWorker), not mute toggle
        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.ScrollLock, IsPressed = false });

        _muteServiceMock.Verify(x => x.Toggle(), Times.Never);
    }

    [Fact]
    public void OnKeyReleased_AnyKey_DoesNotToggleMute()
    {
        // Mute toggle via keyboard has been removed
        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.CapsLock, IsPressed = false });
        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.Escape, IsPressed = false });
        _capturedKeyReleasedHandler?.Invoke(this, new KeyEventArgs { Key = KeyCode.NumLock, IsPressed = false });

        _muteServiceMock.Verify(x => x.Toggle(), Times.Never);
    }

    #endregion

    public void Dispose()
    {
        _sut?.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
    }
}
