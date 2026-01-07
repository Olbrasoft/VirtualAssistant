using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Data.Dtos.Clipboard;
using Olbrasoft.VirtualAssistant.Service.Controllers;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Controllers;

/// <summary>
/// Unit tests for ClipboardController.
/// Note: Tests focus on validation logic. Actual xclip execution is not tested
/// as it requires external process and system clipboard.
/// </summary>
public class ClipboardControllerTests
{
    private readonly Mock<ILogger<ClipboardController>> _loggerMock;
    private readonly ClipboardController _controller;

    public ClipboardControllerTests()
    {
        _loggerMock = new Mock<ILogger<ClipboardController>>();
        _controller = new ClipboardController(_loggerMock.Object);
    }

    [Fact]
    public async Task ToClipboard_WithEmptyContent_ReturnsBadRequest()
    {
        // Arrange
        var request = new ClipboardRequest { Content = string.Empty };

        // Act
        var result = await _controller.ToClipboard(request, CancellationToken.None);

        // Assert
        var badResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var error = Assert.IsType<ErrorResponse>(badResult.Value);
        Assert.Equal("Content cannot be empty", error.Error);
    }

    [Fact]
    public async Task ToClipboard_WithNullContent_ReturnsBadRequest()
    {
        // Arrange
        var request = new ClipboardRequest { Content = null! };

        // Act
        var result = await _controller.ToClipboard(request, CancellationToken.None);

        // Assert
        var badResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var error = Assert.IsType<ErrorResponse>(badResult.Value);
        Assert.Equal("Content cannot be empty", error.Error);
    }
}
