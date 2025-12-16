using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Service.Controllers;
using VirtualAssistant.Core.Services;

namespace VirtualAssistant.Service.Tests.Controllers;

public class NotificationsControllerTests
{
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<INotificationBatchingService> _batchingServiceMock;
    private readonly Mock<ILogger<NotificationsController>> _loggerMock;
    private readonly NotificationsController _controller;

    public NotificationsControllerTests()
    {
        _notificationServiceMock = new Mock<INotificationService>();
        _batchingServiceMock = new Mock<INotificationBatchingService>();
        _loggerMock = new Mock<ILogger<NotificationsController>>();

        _controller = new NotificationsController(
            _notificationServiceMock.Object,
            _batchingServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CreateNotification_WithValidRequest_ReturnsOkWithId()
    {
        // Arrange
        var request = new CreateNotificationRequest
        {
            Text = "Test notification",
            Source = "claude-code"
        };
        _notificationServiceMock
            .Setup(x => x.CreateNotificationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        // Act
        var result = await _controller.CreateNotification(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        _notificationServiceMock.Verify(
            x => x.CreateNotificationAsync("Test notification", "claude-code", null, It.IsAny<CancellationToken>()),
            Times.Once);
        _batchingServiceMock.Verify(
            x => x.QueueNotification(It.Is<AgentNotification>(n =>
                n.Agent == "claude-code" &&
                n.Content == "Test notification" &&
                n.NotificationId == 42)),
            Times.Once);
    }

    [Fact]
    public async Task CreateNotification_WithIssueIds_PassesIssueIdsToService()
    {
        // Arrange
        var request = new CreateNotificationRequest
        {
            Text = "Working on issues",
            Source = "claude-code",
            IssueIds = [123, 456]
        };
        _notificationServiceMock
            .Setup(x => x.CreateNotificationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _controller.CreateNotification(request, CancellationToken.None);

        // Assert
        Assert.IsType<OkObjectResult>(result);

        _notificationServiceMock.Verify(
            x => x.CreateNotificationAsync(
                "Working on issues",
                "claude-code",
                It.Is<IReadOnlyList<int>>(ids => ids.Count == 2 && ids.Contains(123) && ids.Contains(456)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateNotification_WithEmptyText_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateNotificationRequest
        {
            Text = "",
            Source = "claude-code"
        };

        // Act
        var result = await _controller.CreateNotification(request, CancellationToken.None);

        // Assert
        var badResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badResult.Value);
        Assert.Equal("Text is required", error.Error);

        _notificationServiceMock.Verify(
            x => x.CreateNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateNotification_WithWhitespaceText_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateNotificationRequest
        {
            Text = "   ",
            Source = "claude-code"
        };

        // Act
        var result = await _controller.CreateNotification(request, CancellationToken.None);

        // Assert
        var badResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badResult.Value);
        Assert.Equal("Text is required", error.Error);
    }

    [Fact]
    public async Task CreateNotification_WithNullSource_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateNotificationRequest
        {
            Text = "Valid text",
            Source = null
        };

        // Act
        var result = await _controller.CreateNotification(request, CancellationToken.None);

        // Assert
        var badResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badResult.Value);
        Assert.Equal("Source (agent name) is required", error.Error);
    }

    [Fact]
    public async Task CreateNotification_WithEmptySource_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateNotificationRequest
        {
            Text = "Valid text",
            Source = ""
        };

        // Act
        var result = await _controller.CreateNotification(request, CancellationToken.None);

        // Assert
        var badResult = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badResult.Value);
        Assert.Equal("Source (agent name) is required", error.Error);
    }
}
