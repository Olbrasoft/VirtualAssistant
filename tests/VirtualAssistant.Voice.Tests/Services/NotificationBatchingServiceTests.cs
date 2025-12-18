using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Services;
using VirtualAssistant.Core.Services;
using VirtualAssistant.Data.Enums;
using VirtualAssistant.GitHub.Configuration;
using VirtualAssistant.GitHub.Services;

namespace VirtualAssistant.Voice.Tests.Services;

/// <summary>
/// Unit tests for NotificationBatchingService.
/// Tests verify that humanization is skipped when no issues are associated.
/// </summary>
public class NotificationBatchingServiceTests : IDisposable
{
    private readonly Mock<ILogger<NotificationBatchingService>> _loggerMock;
    private readonly Mock<IHumanizationService> _humanizationServiceMock;
    private readonly Mock<IVirtualAssistantSpeaker> _speakerMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<IIssueSummaryClient> _issueSummaryClientMock;
    private readonly Mock<ISpeechLockService> _speechLockServiceMock;
    private readonly IOptions<SpeechToTextSettings> _speechToTextSettings;
    private readonly IOptions<GitHubSettings> _gitHubSettings;
    private readonly NotificationBatchingService _sut;

    public NotificationBatchingServiceTests()
    {
        _loggerMock = new Mock<ILogger<NotificationBatchingService>>();
        _humanizationServiceMock = new Mock<IHumanizationService>();
        _speakerMock = new Mock<IVirtualAssistantSpeaker>();
        _notificationServiceMock = new Mock<INotificationService>();
        _issueSummaryClientMock = new Mock<IIssueSummaryClient>();
        _speechLockServiceMock = new Mock<ISpeechLockService>();

        // Default: speech not locked
        _speechLockServiceMock.Setup(x => x.IsLocked).Returns(false);

        _speechToTextSettings = Options.Create(new SpeechToTextSettings
        {
            BaseUrl = "http://localhost:5050",
            StatusTimeoutMs = 1000,
            PollingIntervalMs = 500
        });
        _gitHubSettings = Options.Create(new GitHubSettings
        {
            Owner = "TestOwner",
            DefaultRepo = "TestRepo"
        });

        _sut = new NotificationBatchingService(
            _loggerMock.Object,
            _humanizationServiceMock.Object,
            _speakerMock.Object,
            _notificationServiceMock.Object,
            _issueSummaryClientMock.Object,
            _speechLockServiceMock.Object,
            _speechToTextSettings,
            _gitHubSettings);
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
    public async Task FlushAsync_WithNoIssues_SkipsHumanizationAndSendsDirectlyToTts()
    {
        // Arrange
        var notification = new AgentNotification
        {
            NotificationId = 1,
            Agent = "claude-code",
            Type = "status",
            Content = "Začínám pracovat na úkolu."
        };

        // Setup: no associated issues
        _notificationServiceMock
            .Setup(x => x.GetAssociatedIssueIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<int>());

        _speakerMock
            .Setup(x => x.SpeakAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        _sut.QueueNotification(notification);

        // Act
        await _sut.FlushAsync();

        // Assert
        // Humanization should NOT be called when there are no associated issues
        _humanizationServiceMock.Verify(
            x => x.HumanizeAsync(
                It.IsAny<IReadOnlyList<AgentNotification>>(),
                It.IsAny<IReadOnlyDictionary<int, IssueSummaryInfo>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        // Speaker should be called with the original text
        _speakerMock.Verify(
            x => x.SpeakAsync("Začínám pracovat na úkolu.", "claude-code"),
            Times.Once);
    }

    [Fact]
    public async Task FlushAsync_WithIssues_CallsHumanization()
    {
        // Arrange
        var notification = new AgentNotification
        {
            NotificationId = 1,
            Agent = "claude-code",
            Type = "status",
            Content = "Pracuji na issue 302."
        };

        var issueSummaries = new Dictionary<int, IssueSummary>
        {
            [302] = new IssueSummary
            {
                IssueNumber = 302,
                CzechTitle = "Přeskočit humanizaci",
                CzechSummary = "Optimalizace notifikací",
                IsOpen = true
            }
        };

        // Setup: has associated issues
        _notificationServiceMock
            .Setup(x => x.GetAssociatedIssueIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 302 });

        _issueSummaryClientMock
            .Setup(x => x.GetSummariesAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<int>>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IssueSummariesResult { Summaries = issueSummaries });

        _humanizationServiceMock
            .Setup(x => x.HumanizeAsync(
                It.IsAny<IReadOnlyList<AgentNotification>>(),
                It.IsAny<IReadOnlyDictionary<int, IssueSummaryInfo>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Humanizovaný text o issue 302.");

        _speakerMock
            .Setup(x => x.SpeakAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        _sut.QueueNotification(notification);

        // Act
        await _sut.FlushAsync();

        // Assert
        // Humanization SHOULD be called when there are associated issues
        _humanizationServiceMock.Verify(
            x => x.HumanizeAsync(
                It.IsAny<IReadOnlyList<AgentNotification>>(),
                It.Is<IReadOnlyDictionary<int, IssueSummaryInfo>?>(d => d != null && d.Count > 0),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Speaker should be called with humanized text
        _speakerMock.Verify(
            x => x.SpeakAsync("Humanizovaný text o issue 302.", "claude-code"),
            Times.Once);
    }

    [Fact]
    public async Task FlushAsync_WithMultipleNotificationsNoIssues_CombinesTexts()
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

        // Setup: no associated issues
        _notificationServiceMock
            .Setup(x => x.GetAssociatedIssueIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<int>());

        _speakerMock
            .Setup(x => x.SpeakAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        _sut.QueueNotification(notification1);
        _sut.QueueNotification(notification2);

        // Act
        await _sut.FlushAsync();

        // Assert
        // Texts should be combined with space separator
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
        _humanizationServiceMock.Verify(
            x => x.HumanizeAsync(
                It.IsAny<IReadOnlyList<AgentNotification>>(),
                It.IsAny<IReadOnlyDictionary<int, IssueSummaryInfo>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _speakerMock.Verify(
            x => x.SpeakAsync(It.IsAny<string>(), It.IsAny<string?>()),
            Times.Never);
    }
}
