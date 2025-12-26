using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Events;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Voice.Services;
using Olbrasoft.VirtualAssistant.Voice.Workers;

namespace VirtualAssistant.Voice.Tests.Workers;

/// <summary>
/// Unit tests for AudioCapturerWorker.
/// Tests audio capture and event publishing functionality.
/// </summary>
public class AudioCapturerWorkerTests : IDisposable
{
    private readonly Mock<ILogger<AudioCapturerWorker>> _loggerMock;
    private readonly Mock<IAudioCaptureService> _audioCaptureServiceMock;
    private readonly Mock<IEventBus> _eventBusMock;
    private readonly Mock<IManualMuteService> _muteServiceMock;
    private readonly AudioCapturerWorker _sut;
    private readonly CancellationTokenSource _cts;

    public AudioCapturerWorkerTests()
    {
        _loggerMock = new Mock<ILogger<AudioCapturerWorker>>();
        _audioCaptureServiceMock = new Mock<IAudioCaptureService>();
        _eventBusMock = new Mock<IEventBus>();
        _muteServiceMock = new Mock<IManualMuteService>();
        _cts = new CancellationTokenSource();

        // Default: unmuted
        _muteServiceMock.Setup(x => x.IsMuted).Returns(false);

        _sut = new AudioCapturerWorker(
            _loggerMock.Object,
            _audioCaptureServiceMock.Object,
            _eventBusMock.Object,
            _muteServiceMock.Object);
    }

    public void Dispose()
    {
        _cts?.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_StartsAudioCapture()
    {
        // Arrange
        _audioCaptureServiceMock.Setup(x => x.ReadChunkAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var executeTask = _sut.StartAsync(_cts.Token);
        await Task.Delay(100); // Let it start

        // Act
        _cts.Cancel();
        await executeTask;

        // Assert
        _audioCaptureServiceMock.Verify(x => x.Start(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PublishesAudioChunkEvent_WhenChunkReceived()
    {
        // Arrange
        var audioChunk = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var callCount = 0;

        _audioCaptureServiceMock.Setup(x => x.ReadChunkAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? audioChunk : null;
            });

        AudioChunkCapturedEvent? publishedEvent = null;
        _eventBusMock.Setup(x => x.PublishAsync(It.IsAny<AudioChunkCapturedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AudioChunkCapturedEvent, CancellationToken>((evt, ct) => publishedEvent = evt)
            .Returns(Task.CompletedTask);

        var executeTask = _sut.StartAsync(_cts.Token);
        await Task.Delay(200); // Let it process

        // Act
        _cts.Cancel();
        await executeTask;

        // Assert
        Assert.NotNull(publishedEvent);
        Assert.Equal(audioChunk, publishedEvent.AudioData);
        Assert.True(publishedEvent.Rms > 0);
    }

    [Fact]
    public async Task ExecuteAsync_CalculatesRMS_Correctly()
    {
        // Arrange - 16-bit PCM samples: [100, -100, 200, -200]
        var audioChunk = new byte[]
        {
            100, 0,   // Sample 1: 100
            156, 255, // Sample 2: -100 (two's complement)
            200, 0,   // Sample 3: 200
            56, 255   // Sample 4: -200
        };

        var callCount = 0;
        _audioCaptureServiceMock.Setup(x => x.ReadChunkAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? audioChunk : null;
            });

        AudioChunkCapturedEvent? publishedEvent = null;
        _eventBusMock.Setup(x => x.PublishAsync(It.IsAny<AudioChunkCapturedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<AudioChunkCapturedEvent, CancellationToken>((evt, ct) => publishedEvent = evt)
            .Returns(Task.CompletedTask);

        var executeTask = _sut.StartAsync(_cts.Token);
        await Task.Delay(200);

        // Act
        _cts.Cancel();
        await executeTask;

        // Assert
        Assert.NotNull(publishedEvent);
        // RMS = sqrt((100^2 + 100^2 + 200^2 + 200^2) / 4) = sqrt(100000 / 4) = sqrt(25000) ≈ 158.11
        Assert.InRange(publishedEvent.Rms, 157f, 159f);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsNullChunks()
    {
        // Arrange
        var callCount = 0;
        _audioCaptureServiceMock.Setup(x => x.ReadChunkAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount <= 3 ? null : throw new OperationCanceledException();
            });

        var executeTask = _sut.StartAsync(_cts.Token);
        await Task.Delay(400); // Let it process 3 null chunks

        // Act
        _cts.Cancel();
        await executeTask;

        // Assert - should not publish any events for null chunks
        _eventBusMock.Verify(
            x => x.PublishAsync(It.IsAny<AudioChunkCapturedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_StopsCapture_OnCancellation()
    {
        // Arrange
        _audioCaptureServiceMock.Setup(x => x.ReadChunkAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var executeTask = _sut.StartAsync(_cts.Token);
        await Task.Delay(100);

        // Act
        _cts.Cancel();
        await executeTask;

        // Assert
        _audioCaptureServiceMock.Verify(x => x.Stop(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesInvalidOperationException()
    {
        // Arrange
        _audioCaptureServiceMock.Setup(x => x.ReadChunkAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Audio capture not started"));

        var executeTask = _sut.StartAsync(_cts.Token);
        await Task.Delay(100);

        // Act
        _cts.Cancel();
        await executeTask;

        // Assert - should complete without throwing
        _audioCaptureServiceMock.Verify(x => x.Stop(), Times.Once);
    }
}
