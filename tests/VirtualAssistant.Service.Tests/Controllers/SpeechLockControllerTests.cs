using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Service.Controllers;
using Olbrasoft.VirtualAssistant.Voice.Services;
using VirtualAssistant.Core.Services;

namespace VirtualAssistant.Service.Tests.Controllers;

public class SpeechLockControllerTests
{
    private readonly Mock<ISpeechLockService> _speechLockServiceMock;
    private readonly Mock<IVirtualAssistantSpeaker> _speakerMock;
    private readonly Mock<INotificationBatchingService> _batchingServiceMock;
    private readonly Mock<ILogger<SpeechLockController>> _loggerMock;
    private readonly SpeechLockController _controller;

    public SpeechLockControllerTests()
    {
        _speechLockServiceMock = new Mock<ISpeechLockService>();
        _speakerMock = new Mock<IVirtualAssistantSpeaker>();
        _batchingServiceMock = new Mock<INotificationBatchingService>();
        _loggerMock = new Mock<ILogger<SpeechLockController>>();

        _controller = new SpeechLockController(
            _speechLockServiceMock.Object,
            _speakerMock.Object,
            _batchingServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void Start_WithoutRequest_CancelsSpeechAndLocks()
    {
        // Arrange
        _batchingServiceMock.Setup(x => x.PendingCount).Returns(0);
        _speakerMock.Setup(x => x.QueueCount).Returns(0);

        // Act
        var result = _controller.Start(null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SpeechLockResponse>(okResult.Value);
        Assert.True(response.Success);
        Assert.True(response.IsLocked);
        Assert.Equal("Speech lock activated", response.Message);

        _speakerMock.Verify(x => x.CancelCurrentSpeech(), Times.Once);
        _speechLockServiceMock.Verify(x => x.Lock(null), Times.Once);
    }

    [Fact]
    public void Start_WithCustomTimeout_PassesTimeoutToService()
    {
        // Arrange
        var request = new SpeechLockStartRequest { TimeoutSeconds = 60 };
        _batchingServiceMock.Setup(x => x.PendingCount).Returns(0);
        _speakerMock.Setup(x => x.QueueCount).Returns(0);

        // Act
        var result = _controller.Start(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SpeechLockResponse>(okResult.Value);
        Assert.True(response.Success);

        _speechLockServiceMock.Verify(
            x => x.Lock(It.Is<TimeSpan?>(t => t.HasValue && t.Value.TotalSeconds == 60)),
            Times.Once);
    }

    [Fact]
    public void Start_WithZeroTimeout_PassesNullTimeout()
    {
        // Arrange
        var request = new SpeechLockStartRequest { TimeoutSeconds = 0 };
        _batchingServiceMock.Setup(x => x.PendingCount).Returns(0);
        _speakerMock.Setup(x => x.QueueCount).Returns(0);

        // Act
        var result = _controller.Start(request);

        // Assert
        _speechLockServiceMock.Verify(x => x.Lock(null), Times.Once);
    }

    [Fact]
    public void Start_WithPendingNotifications_ReturnsQueueCount()
    {
        // Arrange
        _batchingServiceMock.Setup(x => x.PendingCount).Returns(3);
        _speakerMock.Setup(x => x.QueueCount).Returns(2);

        // Act
        var result = _controller.Start(null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SpeechLockResponse>(okResult.Value);
        Assert.Equal(5, response.QueueCount); // 3 batching + 2 tts
    }

    [Fact]
    public async Task Stop_UnlocksAndFlushesBothQueues()
    {
        // Arrange
        _batchingServiceMock.Setup(x => x.PendingCount).Returns(2);
        _speakerMock.Setup(x => x.QueueCount).Returns(3);

        // Act
        var result = await _controller.Stop();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SpeechLockResponse>(okResult.Value);
        Assert.True(response.Success);
        Assert.False(response.IsLocked);
        Assert.Contains("5", response.Message); // 2 batching + 3 tts

        _speechLockServiceMock.Verify(x => x.Unlock(), Times.Once);
        _batchingServiceMock.Verify(x => x.FlushAsync(), Times.Once);
        _speakerMock.Verify(x => x.FlushQueueAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Stop_WithNoQueuedMessages_DoesNotFlush()
    {
        // Arrange
        _batchingServiceMock.Setup(x => x.PendingCount).Returns(0);
        _speakerMock.Setup(x => x.QueueCount).Returns(0);

        // Act
        var result = await _controller.Stop();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SpeechLockResponse>(okResult.Value);
        Assert.Equal("Speech lock released", response.Message);

        _speechLockServiceMock.Verify(x => x.Unlock(), Times.Once);
        _batchingServiceMock.Verify(x => x.FlushAsync(), Times.Never);
        _speakerMock.Verify(x => x.FlushQueueAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Stop_WithOnlyTtsQueue_FlushesTtsOnly()
    {
        // Arrange
        _batchingServiceMock.Setup(x => x.PendingCount).Returns(0);
        _speakerMock.Setup(x => x.QueueCount).Returns(2);

        // Act
        var result = await _controller.Stop();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SpeechLockResponse>(okResult.Value);
        Assert.Contains("2", response.Message);

        _batchingServiceMock.Verify(x => x.FlushAsync(), Times.Never);
        _speakerMock.Verify(x => x.FlushQueueAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetStatus_WhenLocked_ReturnsLockedStatus()
    {
        // Arrange
        _speechLockServiceMock.Setup(x => x.IsLocked).Returns(true);
        _batchingServiceMock.Setup(x => x.PendingCount).Returns(5);
        _batchingServiceMock.Setup(x => x.IsProcessing).Returns(false);
        _speakerMock.Setup(x => x.QueueCount).Returns(3);

        // Act
        var result = _controller.GetStatus();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SpeechLockResponse>(okResult.Value);
        Assert.True(response.Success);
        Assert.True(response.IsLocked);
        Assert.Equal("Speech is locked", response.Message);
        Assert.Equal(8, response.QueueCount); // 5 batching + 3 tts
        Assert.Equal(5, response.BatchingQueueCount);
        Assert.Equal(3, response.TtsQueueCount);
        Assert.False(response.IsProcessing);
    }

    [Fact]
    public void GetStatus_WhenUnlocked_ReturnsUnlockedStatus()
    {
        // Arrange
        _speechLockServiceMock.Setup(x => x.IsLocked).Returns(false);
        _batchingServiceMock.Setup(x => x.PendingCount).Returns(0);
        _batchingServiceMock.Setup(x => x.IsProcessing).Returns(true);
        _speakerMock.Setup(x => x.QueueCount).Returns(0);

        // Act
        var result = _controller.GetStatus();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SpeechLockResponse>(okResult.Value);
        Assert.True(response.Success);
        Assert.False(response.IsLocked);
        Assert.Equal("Speech is unlocked", response.Message);
        Assert.Equal(0, response.QueueCount);
        Assert.True(response.IsProcessing);
    }
}
