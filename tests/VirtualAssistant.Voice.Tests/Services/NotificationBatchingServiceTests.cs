using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Services;
using VirtualAssistant.Core.Services;

namespace VirtualAssistant.Voice.Tests.Services;

/// <summary>
/// Unit tests for NotificationBatchingService.
/// Tests verify that notifications are sent directly to TTS without delay.
/// </summary>
public class NotificationBatchingServiceTests : IDisposable
{
    private readonly Mock<ILogger<NotificationBatchingService>> _loggerMock;
    private readonly Mock<IVirtualAssistantSpeaker> _speakerMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<ISpeechLockService> _speechLockServiceMock;
    private readonly IOptions<SpeechToTextSettings> _speechToTextSettings;
    private readonly NotificationBatchingService _sut;

    public NotificationBatchingServiceTests()
    {
        _loggerMock = new Mock<ILogger<NotificationBatchingService>>();
        _speakerMock = new Mock<IVirtualAssistantSpeaker>();
        _notificationServiceMock = new Mock<INotificationService>();
        _speechLockServiceMock = new Mock<ISpeechLockService>();

        // Default: speech not locked
        _speechLockServiceMock.Setup(x => x.IsLocked).Returns(false);

        _speechToTextSettings = Options.Create(new SpeechToTextSettings
        {
            BaseUrl = "http://localhost:5050",
            StatusTimeoutMs = 1000,
            PollingIntervalMs = 500
        });

        _sut = new NotificationBatchingService(
            _loggerMock.Object,
            _speakerMock.Object,
            _notificationServiceMock.Object,
            _speechLockServiceMock.Object,
            _speechToTextSettings);
    }

    public void Dispose()
    {
        _sut.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void PendingCount_InitialState_ReturnsZero()
    {
        // Act
        var result = _sut.PendingCount;

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void IsProcessing_InitialState_ReturnsFalse()
    {
        // Act
        var result = _sut.IsProcessing;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task QueueNotification_ProcessesImmediately()
    {
        // Arrange
        var notification = new AgentNotification
        {
            Agent = "test-agent",
            Type = "status",
            Content = "Test notification"
        };

        _speakerMock
            .Setup(x => x.SpeakAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        // Act
        _sut.QueueNotification(notification);

        // Wait a bit for async processing to complete
        await Task.Delay(100);

        // Assert - notification was processed immediately (queue is empty)
        Assert.Equal(0, _sut.PendingCount);
        _speakerMock.Verify(
            x => x.SpeakAsync("Test notification", "test-agent"),
            Times.Once);
    }

    [Fact]
    public async Task FlushAsync_SendsTextDirectlyToTts()
    {
        // Arrange
        var notification = new AgentNotification
        {
            NotificationId = 1,
            Agent = "claude-code",
            Type = "status",
            Content = "Začínám pracovat na úkolu."
        };

        // Block speaker to prevent immediate processing
        var speakerTcs = new TaskCompletionSource();
        _speakerMock
            .Setup(x => x.SpeakAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(speakerTcs.Task);

        _sut.QueueNotification(notification);

        // Complete the speaker task
        speakerTcs.SetResult();

        // Act
        await _sut.FlushAsync();

        // Assert - text goes directly to TTS
        _speakerMock.Verify(
            x => x.SpeakAsync("Začínám pracovat na úkolu.", "claude-code"),
            Times.Once);
    }

    [Fact]
    public async Task QueueNotification_WithMultipleNotifications_ProcessesSequentially()
    {
        // Arrange
        var processedTexts = new List<string>();

        _speakerMock
            .Setup(x => x.SpeakAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string?, CancellationToken>((text, _, _) => processedTexts.Add(text))
            .Returns(Task.CompletedTask);

        var notification1 = new AgentNotification
        {
            NotificationId = 1,
            Agent = "claude-code",
            Type = "status",
            Content = "První část."
        };
        var notification2 = new AgentNotification
        {
            NotificationId = 2,
            Agent = "claude-code",
            Type = "status",
            Content = "Druhá část."
        };

        // Act
        _sut.QueueNotification(notification1);
        _sut.QueueNotification(notification2);

        // Wait for async processing
        await Task.Delay(200);

        // Assert - each notification is processed separately
        Assert.Equal(2, processedTexts.Count);
        Assert.Contains("První část.", processedTexts);
        Assert.Contains("Druhá část.", processedTexts);
    }

    [Fact]
    public async Task FlushAsync_WithEmptyQueue_DoesNothing()
    {
        // Act
        await _sut.FlushAsync();

        // Assert
        _speakerMock.Verify(
            x => x.SpeakAsync(It.IsAny<string>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task QueueNotification_WhenSpeechLocked_WaitsForUnlock()
    {
        // Arrange
        var lockSequence = new Queue<bool>(new[] { true, true, false }); // Locked twice, then unlocked
        _speechLockServiceMock
            .Setup(x => x.IsLocked)
            .Returns(() => lockSequence.Count > 0 ? lockSequence.Dequeue() : false);

        _speakerMock
            .Setup(x => x.SpeakAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        var notification = new AgentNotification
        {
            Agent = "test-agent",
            Type = "status",
            Content = "Test notification"
        };

        // Act
        _sut.QueueNotification(notification);

        // Wait for async processing (including polling delays)
        await Task.Delay(1500);

        // Assert - notification was eventually processed
        _speakerMock.Verify(
            x => x.SpeakAsync("Test notification", "test-agent"),
            Times.Once);
    }
}
