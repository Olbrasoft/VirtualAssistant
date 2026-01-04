using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using VirtualAssistant.Core.Models;
using VirtualAssistant.Core.Services;
using VirtualAssistant.Desktop.Configuration;
using VirtualAssistant.Desktop.Services;
using Xunit;

namespace VirtualAssistant.Desktop.Tests.Services;

public class ContextAwareNotificationFilterTests
{
    private readonly Mock<ILogger<ContextAwareNotificationFilter>> _loggerMock;
    private readonly NotificationFilteringOptions _options;
    private readonly INotificationFilter _sut;

    public ContextAwareNotificationFilterTests()
    {
        _loggerMock = new Mock<ILogger<ContextAwareNotificationFilter>>();
        _options = new NotificationFilteringOptions
        {
            Enabled = true,
            AppNameMapping = new Dictionary<string, string>
            {
                ["Claude Code"] = "code",
                ["OpenCode"] = "code",
                ["VS Code"] = "code",
                ["GitHub"] = "chrome",
                ["PyCharm"] = "pycharm",
                ["Rider"] = "rider"
            },
            AlwaysDeliverSources = new[] { NotificationSource.SystemAlert, NotificationSource.UserMessage }
        };

        var optionsMock = new Mock<IOptions<NotificationFilteringOptions>>();
        optionsMock.Setup(x => x.Value).Returns(_options);

        _sut = new ContextAwareNotificationFilter(optionsMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ShouldDeliverAsync_UserInTargetApp_ReturnsFalse()
    {
        // Arrange
        var context = new DesktopContext(
            CurrentWorkspace: 1,
            TotalWorkspaces: 4,
            ActiveWindowTitle: "main.py - Code",
            ActiveWindowClass: "code",
            ActiveApplication: "code",
            Timestamp: DateTime.UtcNow
        );

        // Act
        var result = await _sut.ShouldDeliverAsync(
            "Claude Code dokončil práci na issue 42",
            context
        );

        // Assert
        Assert.False(result); // Should skip notification
    }

    [Fact]
    public async Task ShouldDeliverAsync_UserInDifferentApp_ReturnsTrue()
    {
        // Arrange
        var context = new DesktopContext(
            CurrentWorkspace: 1,
            TotalWorkspaces: 4,
            ActiveWindowTitle: "WhatsApp",
            ActiveWindowClass: "whatsapp-for-linux",
            ActiveApplication: "whatsapp-for-linux",
            Timestamp: DateTime.UtcNow
        );

        // Act
        var result = await _sut.ShouldDeliverAsync(
            "Claude Code dokončil práci na issue 42",
            context
        );

        // Assert
        Assert.True(result); // Should deliver notification
    }

    [Fact]
    public async Task ShouldDeliverAsync_GitHubNotificationInBrowser_ReturnsFalse()
    {
        // Arrange
        var context = new DesktopContext(
            CurrentWorkspace: 1,
            TotalWorkspaces: 4,
            ActiveWindowTitle: "GitHub - Chrome",
            ActiveWindowClass: "Chrome",
            ActiveApplication: "chrome",
            Timestamp: DateTime.UtcNow
        );

        // Act
        var result = await _sut.ShouldDeliverAsync(
            "GitHub: New issue created in VirtualAssistant",
            context
        );

        // Assert
        Assert.False(result); // Should skip (user already in Chrome/GitHub)
    }

    [Fact]
    public async Task ShouldDeliverAsync_UrgentNotification_AlwaysReturnsTrue()
    {
        // Arrange
        var context = new DesktopContext(
            CurrentWorkspace: 1,
            TotalWorkspaces: 4,
            ActiveWindowTitle: "Code",
            ActiveWindowClass: "code",
            ActiveApplication: "code",
            Timestamp: DateTime.UtcNow
        );

        // Act
        var result = await _sut.ShouldDeliverAsync(
            "URGENT: Claude Code encountered critical error",
            context
        );

        // Assert
        Assert.True(result); // Always deliver urgent notifications
    }

    [Theory]
    [InlineData("critical system failure")]
    [InlineData("ERROR: Build failed")]
    [InlineData("Urgent notification")]
    public async Task ShouldDeliverAsync_UrgentKeywords_AlwaysReturnsTrue(string notificationText)
    {
        // Arrange
        var context = new DesktopContext(
            CurrentWorkspace: 1,
            TotalWorkspaces: 4,
            ActiveWindowTitle: "Code",
            ActiveWindowClass: "code",
            ActiveApplication: "code",
            Timestamp: DateTime.UtcNow
        );

        // Act
        var result = await _sut.ShouldDeliverAsync(notificationText, context);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ShouldDeliverAsync_ContextUnavailable_ReturnsTrue()
    {
        // Act
        var result = await _sut.ShouldDeliverAsync(
            "Claude Code dokončil práci",
            context: null // No context available
        );

        // Assert
        Assert.True(result); // Safe fallback: always deliver
    }

    [Fact]
    public async Task ShouldDeliverAsync_FilteringDisabled_AlwaysReturnsTrue()
    {
        // Arrange
        _options.Enabled = false;

        var context = new DesktopContext(
            CurrentWorkspace: 1,
            TotalWorkspaces: 4,
            ActiveWindowTitle: "Code",
            ActiveWindowClass: "code",
            ActiveApplication: "code",
            Timestamp: DateTime.UtcNow
        );

        // Act
        var result = await _sut.ShouldDeliverAsync(
            "Claude Code dokončil práci",
            context
        );

        // Assert
        Assert.True(result); // Filtering disabled, deliver all
    }

    [Fact]
    public async Task ShouldDeliverAsync_SystemAlertSource_AlwaysReturnsTrue()
    {
        // Arrange
        var context = new DesktopContext(
            CurrentWorkspace: 1,
            TotalWorkspaces: 4,
            ActiveWindowTitle: "Code",
            ActiveWindowClass: "code",
            ActiveApplication: "code",
            Timestamp: DateTime.UtcNow
        );

        // Act
        var result = await _sut.ShouldDeliverAsync(
            "System alert: Disk space low",
            context
        );

        // Assert
        Assert.True(result); // SystemAlert always delivered
    }

    [Fact]
    public async Task ShouldDeliverAsync_UserMessageSource_AlwaysReturnsTrue()
    {
        // Arrange
        var context = new DesktopContext(
            CurrentWorkspace: 1,
            TotalWorkspaces: 4,
            ActiveWindowTitle: "Code",
            ActiveWindowClass: "code",
            ActiveApplication: "code",
            Timestamp: DateTime.UtcNow
        );

        // Act
        var result = await _sut.ShouldDeliverAsync(
            "User message: Meeting in 5 minutes",
            context
        );

        // Assert
        Assert.True(result); // UserMessage always delivered
    }

    [Fact]
    public void ExtractContext_WithAppMention_ExtractsTargetApp()
    {
        // Act
        var context = _sut.ExtractContext("Claude Code dokončil práci na issue 42");

        // Assert
        Assert.Equal("Claude Code", context.TargetApplication);
        Assert.False(context.IsUrgent);
        Assert.Equal(NotificationSource.TaskCompletion, context.Source);
    }

    [Fact]
    public void ExtractContext_WithGitHubMention_ExtractsGitHub()
    {
        // Act
        var context = _sut.ExtractContext("GitHub: New issue created");

        // Assert
        Assert.Equal("GitHub", context.TargetApplication);
        Assert.False(context.IsUrgent);
        Assert.Equal(NotificationSource.GitHubEvent, context.Source);
    }

    [Fact]
    public void ExtractContext_WithUrgentKeyword_SetsUrgentFlag()
    {
        // Act
        var context = _sut.ExtractContext("URGENT: Build failed");

        // Assert
        Assert.True(context.IsUrgent);
    }

    [Fact]
    public void ExtractContext_WithCriticalKeyword_SetsUrgentFlag()
    {
        // Act
        var context = _sut.ExtractContext("Critical error in deployment");

        // Assert
        Assert.True(context.IsUrgent);
    }

    [Fact]
    public void ExtractContext_WithErrorKeyword_SetsUrgentFlag()
    {
        // Act
        var context = _sut.ExtractContext("Error: Connection timeout");

        // Assert
        Assert.True(context.IsUrgent);
    }

    [Theory]
    [InlineData("Claude Code completed task", NotificationSource.TaskCompletion)]
    [InlineData("Claude Code dokončil práci", NotificationSource.TaskCompletion)]
    [InlineData("GitHub issue created", NotificationSource.GitHubEvent)]
    [InlineData("System alert: Low memory", NotificationSource.SystemAlert)]
    [InlineData("Just a regular message", NotificationSource.UserMessage)]
    public void ExtractContext_DetectsCorrectSource(string text, NotificationSource expectedSource)
    {
        // Act
        var context = _sut.ExtractContext(text);

        // Assert
        Assert.Equal(expectedSource, context.Source);
    }

    [Fact]
    public void ExtractContext_NoAppMention_ReturnsNullTargetApp()
    {
        // Act
        var context = _sut.ExtractContext("Just a generic notification");

        // Assert
        Assert.Null(context.TargetApplication);
    }

    [Theory]
    [InlineData("claude code", "code")] // Lowercase
    [InlineData("CLAUDE CODE", "code")] // Uppercase
    [InlineData("Claude Code", "code")] // Mixed case
    public async Task ShouldDeliverAsync_CaseInsensitiveMatching(string appMention, string activeApp)
    {
        // Arrange
        var context = new DesktopContext(
            CurrentWorkspace: 1,
            TotalWorkspaces: 4,
            ActiveWindowTitle: "Code",
            ActiveWindowClass: "code",
            ActiveApplication: activeApp,
            Timestamp: DateTime.UtcNow
        );

        // Act
        var result = await _sut.ShouldDeliverAsync(
            $"{appMention} dokončil práci",
            context
        );

        // Assert
        Assert.False(result); // Should skip regardless of case
    }

    [Fact]
    public async Task ShouldDeliverAsync_LogsSkippedNotification()
    {
        // Arrange
        var context = new DesktopContext(
            CurrentWorkspace: 1,
            TotalWorkspaces: 4,
            ActiveWindowTitle: "Code",
            ActiveWindowClass: "code",
            ActiveApplication: "code",
            Timestamp: DateTime.UtcNow
        );

        // Act
        await _sut.ShouldDeliverAsync("Claude Code dokončil práci", context);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("skipping notification")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ShouldDeliverAsync_LogsUrgentNotification()
    {
        // Arrange
        var context = new DesktopContext(
            CurrentWorkspace: 1,
            TotalWorkspaces: 4,
            ActiveWindowTitle: "Code",
            ActiveWindowClass: "code",
            ActiveApplication: "code",
            Timestamp: DateTime.UtcNow
        );

        // Act
        await _sut.ShouldDeliverAsync("URGENT: Critical error", context);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Urgent notification")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
