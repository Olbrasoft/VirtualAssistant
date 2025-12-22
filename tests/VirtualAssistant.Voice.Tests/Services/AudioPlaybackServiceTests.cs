using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.NotificationAudio.Abstractions;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace VirtualAssistant.Voice.Tests.Services;

/// <summary>
/// Unit tests for AudioPlaybackService with NotificationAudio integration.
/// </summary>
public class AudioPlaybackServiceTests : IDisposable
{
    private readonly Mock<ILogger<AudioPlaybackService>> _loggerMock;
    private readonly Mock<ISpeechLockService> _speechLockServiceMock;
    private readonly Mock<INotificationPlayer> _playerMock;
    private readonly AudioPlaybackService _sut;

    public AudioPlaybackServiceTests()
    {
        _loggerMock = new Mock<ILogger<AudioPlaybackService>>();
        _speechLockServiceMock = new Mock<ISpeechLockService>();
        _playerMock = new Mock<INotificationPlayer>();

        // Default: speech lock is not acquired
        _speechLockServiceMock.Setup(x => x.IsLocked).Returns(false);

        _sut = new AudioPlaybackService(
            _playerMock.Object,
            _loggerMock.Object,
            _speechLockServiceMock.Object);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    [Fact]
    public void IsPlaying_InitialState_ReturnsFalse()
    {
        // Assert
        Assert.False(_sut.IsPlaying);
    }

    [Fact]
    public async Task PlayAsync_ValidFile_CallsPlayerPlayAsync()
    {
        // Arrange
        var audioFile = "/tmp/test.mp3";
        _playerMock
            .Setup(x => x.PlayAsync(audioFile, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.PlayAsync(audioFile);

        // Assert
        _playerMock.Verify(x => x.PlayAsync(audioFile, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PlayAsync_WhenSpeechLockAcquired_StopsPlayback()
    {
        // Arrange
        var audioFile = "/tmp/test.mp3";
        var tcs = new TaskCompletionSource();

        _playerMock
            .Setup(x => x.PlayAsync(audioFile, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await tcs.Task; // Keep playing until we complete the task
            });

        // Start with unlocked, then acquire lock after playback starts
        var callCount = 0;
        _speechLockServiceMock
            .Setup(x => x.IsLocked)
            .Returns(() =>
            {
                callCount++;
                if (callCount > 2)
                {
                    tcs.TrySetResult(); // Complete playback when lock is checked
                    return true; // Lock acquired
                }
                return false;
            });

        // Act
        await _sut.PlayAsync(audioFile);

        // Assert - Stop should be called when lock is acquired
        _playerMock.Verify(x => x.Stop(), Times.Once);
    }

    [Fact]
    public void Stop_WhenPlaying_CallsPlayerStop()
    {
        // Arrange
        var audioFile = "/tmp/test.mp3";
        var tcs = new TaskCompletionSource();

        _playerMock
            .Setup(x => x.PlayAsync(audioFile, It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        // Start playback in background
        _ = Task.Run(async () => await _sut.PlayAsync(audioFile));

        // Wait a bit for playback to start
        Thread.Sleep(100);

        // Act
        _sut.Stop();

        // Complete the playback task
        tcs.SetResult();

        // Assert
        _playerMock.Verify(x => x.Stop(), Times.Once);
    }

    [Fact]
    public void Stop_WhenNotPlaying_DoesNotThrow()
    {
        // Act & Assert - should not throw
        var exception = Record.Exception(() => _sut.Stop());
        Assert.Null(exception);
    }

    [Fact]
    public async Task PlayAsync_WhenCancelled_StopsPlayback()
    {
        // Arrange
        var audioFile = "/tmp/test.mp3";
        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource();

        _playerMock
            .Setup(x => x.PlayAsync(audioFile, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await tcs.Task;
            });

        // Act
        var playTask = Task.Run(async () =>
        {
            try
            {
                await _sut.PlayAsync(audioFile, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        });

        // Wait a bit for playback to start
        await Task.Delay(100);

        // Cancel playback
        cts.Cancel();

        // Complete the mock playback
        tcs.SetResult();

        // Wait for completion
        await playTask;

        // Assert
        _playerMock.Verify(x => x.Stop(), Times.Once);
    }

    [Fact]
    public async Task IsPlaying_DuringPlayback_ReturnsTrue()
    {
        // Arrange
        var audioFile = "/tmp/test.mp3";
        var tcs = new TaskCompletionSource();

        _playerMock
            .Setup(x => x.PlayAsync(audioFile, It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        // Act - start playback in background
        var playTask = Task.Run(async () => await _sut.PlayAsync(audioFile));

        // Wait for playback to start
        await Task.Delay(100);

        // Assert - should be playing
        Assert.True(_sut.IsPlaying);

        // Complete playback
        tcs.SetResult();
        await playTask;

        // Assert - should not be playing anymore
        Assert.False(_sut.IsPlaying);
    }

    [Fact]
    public void Dispose_StopsPlayback()
    {
        // Arrange
        var audioFile = "/tmp/test.mp3";
        var tcs = new TaskCompletionSource();

        _playerMock
            .Setup(x => x.PlayAsync(audioFile, It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        // Start playback
        _ = Task.Run(async () =>
        {
            try
            {
                await _sut.PlayAsync(audioFile);
            }
            catch (OperationCanceledException)
            {
                // Expected when disposed
            }
        });

        Thread.Sleep(100);

        // Act
        _sut.Dispose();

        // Complete playback
        tcs.SetResult();

        // Assert
        _playerMock.Verify(x => x.Stop(), Times.Once);
    }

    [Fact]
    public async Task PlayAsync_MonitorsSpeechLockEvery50Ms()
    {
        // Arrange
        var audioFile = "/tmp/test.mp3";
        var checkCount = 0;
        var tcs = new TaskCompletionSource();

        _playerMock
            .Setup(x => x.PlayAsync(audioFile, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                // Simulate playback taking 200ms
                await Task.Delay(200);
            });

        _speechLockServiceMock
            .Setup(x => x.IsLocked)
            .Returns(() =>
            {
                checkCount++;
                return false; // Never locked
            });

        // Act
        await _sut.PlayAsync(audioFile);

        // Assert - should check lock multiple times during 200ms playback
        // At least 3-4 checks (200ms / 50ms = 4 checks)
        Assert.True(checkCount >= 3, $"Expected at least 3 lock checks, but got {checkCount}");
    }
}
