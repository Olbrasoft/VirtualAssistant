using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Services;
using VirtualAssistant.Core.Services;

namespace VirtualAssistant.Voice.Tests.Services;

/// <summary>
/// Unit tests for NotificationBatchingService.
/// Tests verify that notifications are sent directly to TTS without humanization.
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
    public void QueueNotification_IncrementsPendingCount()
    {
        // Arrange
        var notification = new AgentNotification
        {
            Agent = "test-agent",
            Type = "status",
            Content = "Test notification"
        };

        // Act
        _sut.QueueNotification(notification);

        // Assert
        Assert.Equal(1, _sut.PendingCount);
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

        _speakerMock
            .Setup(x => x.SpeakAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        _sut.QueueNotification(notification);

        // Act
        await _sut.FlushAsync();

        // Assert - text goes directly to TTS without humanization
        _speakerMock.Verify(
            x => x.SpeakAsync("Začínám pracovat na úkolu.", "claude-code"),
            Times.Once);
    }

    [Fact]
    public async Task FlushAsync_WithMultipleNotifications_CombinesTexts()
    {
        // Arrange
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

        _speakerMock
            .Setup(x => x.SpeakAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        _sut.QueueNotification(notification1);
        _sut.QueueNotification(notification2);

        // Act
        await _sut.FlushAsync();

        // Assert - texts should be combined with space separator
        _speakerMock.Verify(
            x => x.SpeakAsync("První část. Druhá část.", "claude-code"),
            Times.Once);
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
}
