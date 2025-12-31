using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Service.Services;
using VirtualAssistant.Core.Services;
using VirtualAssistant.Data.Enums;

namespace VirtualAssistant.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="NotificationTracker"/>.
/// Verifies notification lifecycle tracking and database status updates.
/// </summary>
public class NotificationTrackerTests
{
    private readonly Mock<ILogger<NotificationTracker>> _loggerMock;
    private readonly Mock<INotificationService> _notificationServiceMock;

    public NotificationTrackerTests()
    {
        _loggerMock = new Mock<ILogger<NotificationTracker>>();
        _notificationServiceMock = new Mock<INotificationService>();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new NotificationTracker(null!, _notificationServiceMock.Object));
        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullNotificationService_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new NotificationTracker(_loggerMock.Object, null!));
        Assert.Equal("notificationService", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var tracker = new NotificationTracker(_loggerMock.Object, _notificationServiceMock.Object);

        // Assert
        Assert.NotNull(tracker);
    }

    #endregion

    #region MarkAsProcessingAsync Tests

    [Fact]
    public async Task MarkAsProcessingAsync_CallsUpdateStatusAsync_WithProcessingStatus()
    {
        // Arrange
        var tracker = CreateTracker();
        var notificationId = 123;

        // Act
        await tracker.MarkAsProcessingAsync(notificationId);

        // Assert
        _notificationServiceMock.Verify(
            x => x.UpdateStatusAsync(
                It.Is<int[]>(ids => ids.Length == 1 && ids[0] == notificationId),
                NotificationStatusEnum.Processing),
            Times.Once);
    }

    [Fact]
    public async Task MarkAsProcessingAsync_WithDifferentId_CallsUpdateStatusAsync()
    {
        // Arrange
        var tracker = CreateTracker();
        var notificationId = 456;

        // Act
        await tracker.MarkAsProcessingAsync(notificationId);

        // Assert
        _notificationServiceMock.Verify(
            x => x.UpdateStatusAsync(
                It.Is<int[]>(ids => ids.Length == 1 && ids[0] == notificationId),
                NotificationStatusEnum.Processing),
            Times.Once);
    }

    #endregion

    #region MarkAsPlayedAsync Tests

    [Fact]
    public async Task MarkAsPlayedAsync_CallsUpdateStatusAsync_WithPlayedStatus()
    {
        // Arrange
        var tracker = CreateTracker();
        var notificationId = 789;

        // Act
        await tracker.MarkAsPlayedAsync(notificationId);

        // Assert
        _notificationServiceMock.Verify(
            x => x.UpdateStatusAsync(
                It.Is<int[]>(ids => ids.Length == 1 && ids[0] == notificationId),
                NotificationStatusEnum.Played),
            Times.Once);
    }

    [Fact]
    public async Task MarkAsPlayedAsync_WithDifferentId_CallsUpdateStatusAsync()
    {
        // Arrange
        var tracker = CreateTracker();
        var notificationId = 999;

        // Act
        await tracker.MarkAsPlayedAsync(notificationId);

        // Assert
        _notificationServiceMock.Verify(
            x => x.UpdateStatusAsync(
                It.Is<int[]>(ids => ids.Length == 1 && ids[0] == notificationId),
                NotificationStatusEnum.Played),
            Times.Once);
    }

    #endregion

    #region RecordTtsOutcomeAsync Tests

    [Fact]
    public async Task RecordTtsOutcomeAsync_CallsRecordTtsOutcomeAsync_WithCorrectParameters()
    {
        // Arrange
        var tracker = CreateTracker();
        var notificationId = 111;
        var provider = "AzureTTS";
        var status = "success";
        var durationMs = 1500;

        // Act
        await tracker.RecordTtsOutcomeAsync(notificationId, provider, status, durationMs);

        // Assert
        _notificationServiceMock.Verify(
            x => x.RecordTtsOutcomeAsync(notificationId, provider, status, durationMs),
            Times.Once);
    }

    [Fact]
    public async Task RecordTtsOutcomeAsync_WithNullProvider_CallsRecordTtsOutcomeAsync()
    {
        // Arrange
        var tracker = CreateTracker();
        var notificationId = 222;
        var status = "error";
        var durationMs = (int?)null;

        // Act
        await tracker.RecordTtsOutcomeAsync(notificationId, null, status, durationMs);

        // Assert
        _notificationServiceMock.Verify(
            x => x.RecordTtsOutcomeAsync(notificationId, null, status, durationMs),
            Times.Once);
    }

    [Fact]
    public async Task RecordTtsOutcomeAsync_WithCancelledStatus_CallsRecordTtsOutcomeAsync()
    {
        // Arrange
        var tracker = CreateTracker();
        var notificationId = 333;
        var provider = "EdgeTTS";
        var status = "cancelled";
        var durationMs = 500;

        // Act
        await tracker.RecordTtsOutcomeAsync(notificationId, provider, status, durationMs);

        // Assert
        _notificationServiceMock.Verify(
            x => x.RecordTtsOutcomeAsync(notificationId, provider, status, durationMs),
            Times.Once);
    }

    [Fact]
    public async Task RecordTtsOutcomeAsync_WithSkippedStatus_CallsRecordTtsOutcomeAsync()
    {
        // Arrange
        var tracker = CreateTracker();
        var notificationId = 444;
        var provider = "GoogleTTS";
        var status = "skipped";
        var durationMs = 0;

        // Act
        await tracker.RecordTtsOutcomeAsync(notificationId, provider, status, durationMs);

        // Assert
        _notificationServiceMock.Verify(
            x => x.RecordTtsOutcomeAsync(notificationId, provider, status, durationMs),
            Times.Once);
    }

    #endregion

    #region Multiple Operations Tests

    [Fact]
    public async Task MultipleOperations_CallsEachMethodOnce()
    {
        // Arrange
        var tracker = CreateTracker();
        var notificationId = 555;

        // Act
        await tracker.MarkAsProcessingAsync(notificationId);
        await tracker.RecordTtsOutcomeAsync(notificationId, "AzureTTS", "success", 1000);
        await tracker.MarkAsPlayedAsync(notificationId);

        // Assert
        _notificationServiceMock.Verify(
            x => x.UpdateStatusAsync(
                It.Is<int[]>(ids => ids.Length == 1 && ids[0] == notificationId),
                NotificationStatusEnum.Processing),
            Times.Once);

        _notificationServiceMock.Verify(
            x => x.RecordTtsOutcomeAsync(notificationId, "AzureTTS", "success", 1000),
            Times.Once);

        _notificationServiceMock.Verify(
            x => x.UpdateStatusAsync(
                It.Is<int[]>(ids => ids.Length == 1 && ids[0] == notificationId),
                NotificationStatusEnum.Played),
            Times.Once);
    }

    #endregion

    private NotificationTracker CreateTracker()
    {
        return new NotificationTracker(_loggerMock.Object, _notificationServiceMock.Object);
    }
}
