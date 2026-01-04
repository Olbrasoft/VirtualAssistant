using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using VirtualAssistant.Core.Models;
using VirtualAssistant.Core.Services;
using VirtualAssistant.Desktop.Configuration;
using VirtualAssistant.Desktop.Services;
using Xunit;

namespace VirtualAssistant.Desktop.Tests.Services;

public class ContextPromptSelectorTests
{
    private readonly Mock<ILogger<ContextPromptSelector>> _loggerMock;
    private readonly ContextMappingOptions _options;
    private readonly IContextPromptSelector _sut;

    public ContextPromptSelectorTests()
    {
        _loggerMock = new Mock<ILogger<ContextPromptSelector>>();
        _options = new ContextMappingOptions
        {
            Programming = new[] { "code", "cursor", "rider", "vscode" },
            Chat = new[] { "whatsapp-for-linux", "telegram", "slack" },
            Browsing = new[] { "chrome", "firefox", "edge" }
        };

        var optionsMock = new Mock<IOptions<ContextMappingOptions>>();
        optionsMock.Setup(x => x.Value).Returns(_options);

        _sut = new ContextPromptSelector(optionsMock.Object, _loggerMock.Object);
    }

    [Theory]
    [InlineData("code", ContextType.Programming)]
    [InlineData("cursor", ContextType.Programming)]
    [InlineData("rider", ContextType.Programming)]
    [InlineData("vscode", ContextType.Programming)]
    public void DetectContextType_WithProgrammingApps_ReturnsProgramming(string appId, ContextType expected)
    {
        // Act
        var result = _sut.DetectContextType(appId);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("whatsapp-for-linux", ContextType.Chat)]
    [InlineData("telegram", ContextType.Chat)]
    [InlineData("slack", ContextType.Chat)]
    public void DetectContextType_WithChatApps_ReturnsChat(string appId, ContextType expected)
    {
        // Act
        var result = _sut.DetectContextType(appId);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("chrome", ContextType.Browsing)]
    [InlineData("firefox", ContextType.Browsing)]
    [InlineData("edge", ContextType.Browsing)]
    public void DetectContextType_WithBrowsingApps_ReturnsBrowsing(string appId, ContextType expected)
    {
        // Act
        var result = _sut.DetectContextType(appId);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("unknown-app")]
    [InlineData("")]
    [InlineData("some-random-application")]
    public void DetectContextType_WithUnknownApps_ReturnsGeneral(string appId)
    {
        // Act
        var result = _sut.DetectContextType(appId);

        // Assert
        Assert.Equal(ContextType.General, result);
    }

    [Theory]
    [InlineData("CODE")]
    [InlineData("Code")]
    [InlineData("CoDE")]
    [InlineData("VSCODE")]
    [InlineData("VsCode")]
    public void DetectContextType_IsCaseInsensitive(string appId)
    {
        // Act
        var result = _sut.DetectContextType(appId);

        // Assert
        Assert.Equal(ContextType.Programming, result);
    }

    [Theory]
    [InlineData("microsoft-edge-stable", ContextType.Browsing)] // contains "edge"
    [InlineData("google-chrome", ContextType.Browsing)] // contains "chrome"
    [InlineData("visual-studio-code", ContextType.Programming)] // contains "code"
    public void DetectContextType_SupportsSubstringMatching(string appId, ContextType expected)
    {
        // Act
        var result = _sut.DetectContextType(appId);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task SelectPromptAsync_WithNullContext_ReturnsGeneralPrompt()
    {
        // Act
        var result = await _sut.SelectPromptAsync(null);

        // Assert
        Assert.Equal("general.txt", result);
    }

    [Fact]
    public async Task SelectPromptAsync_WithProgrammingContext_ReturnsProgrammingPrompt()
    {
        // Arrange
        var context = new DesktopContext(
            CurrentWorkspace: 0,
            TotalWorkspaces: 4,
            ActiveWindowTitle: "test.cs - Visual Studio Code",
            ActiveWindowClass: "Code",
            ActiveApplication: "code",
            Timestamp: DateTime.UtcNow
        );

        // Act
        var result = await _sut.SelectPromptAsync(context);

        // Assert
        Assert.Equal("programming.txt", result);
    }

    [Fact]
    public async Task SelectPromptAsync_WithChatContext_ReturnsChatPrompt()
    {
        // Arrange
        var context = new DesktopContext(
            CurrentWorkspace: 0,
            TotalWorkspaces: 4,
            ActiveWindowTitle: "WhatsApp",
            ActiveWindowClass: "WhatsApp",
            ActiveApplication: "whatsapp-for-linux",
            Timestamp: DateTime.UtcNow
        );

        // Act
        var result = await _sut.SelectPromptAsync(context);

        // Assert
        Assert.Equal("chat.txt", result);
    }

    [Fact]
    public async Task SelectPromptAsync_WithBrowsingContext_ReturnsSearchPrompt()
    {
        // Arrange
        var context = new DesktopContext(
            CurrentWorkspace: 0,
            TotalWorkspaces: 4,
            ActiveWindowTitle: "Google - Chrome",
            ActiveWindowClass: "Chrome",
            ActiveApplication: "chrome",
            Timestamp: DateTime.UtcNow
        );

        // Act
        var result = await _sut.SelectPromptAsync(context);

        // Assert
        Assert.Equal("search.txt", result);
    }

    [Fact]
    public async Task SelectPromptAsync_WithUnknownContext_ReturnsGeneralPrompt()
    {
        // Arrange
        var context = new DesktopContext(
            CurrentWorkspace: 0,
            TotalWorkspaces: 4,
            ActiveWindowTitle: "Some Unknown App",
            ActiveWindowClass: "UnknownApp",
            ActiveApplication: "unknown-app",
            Timestamp: DateTime.UtcNow
        );

        // Act
        var result = await _sut.SelectPromptAsync(context);

        // Assert
        Assert.Equal("general.txt", result);
    }

    [Fact]
    public async Task SelectPromptAsync_LogsPromptSelection()
    {
        // Arrange
        var context = new DesktopContext(
            CurrentWorkspace: 0,
            TotalWorkspaces: 4,
            ActiveWindowTitle: "test.cs - Code",
            ActiveWindowClass: "Code",
            ActiveApplication: "code",
            Timestamp: DateTime.UtcNow
        );

        // Act
        await _sut.SelectPromptAsync(context);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("programming.txt")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
