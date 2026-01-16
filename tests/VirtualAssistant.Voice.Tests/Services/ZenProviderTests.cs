using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Tests.Services;

public class ZenProviderTests
{
    private readonly Mock<IOptions<ZenOptions>> _optionsMock;
    private readonly Mock<IPromptCache> _promptCacheMock;
    private readonly Mock<ILogger<ZenProvider>> _loggerMock;
    private readonly Mock<IDesktopContextService> _desktopContextServiceMock;
    private readonly Mock<IQueryProcessor> _queryProcessorMock;
    private readonly ZenOptions _options;

    public ZenProviderTests()
    {
        _options = new ZenOptions
        {
            ApiKey = "test-api-key",
            BaseUrl = "https://opencode.ai/zen/v1",
            Model = "alpha-glm-4.7",
            TimeoutSeconds = 30,
            MaxTokens = 1000,
            Temperature = 0.3,
            MinTextLengthForCorrection = 21,
            Enabled = true
        };

        _optionsMock = new Mock<IOptions<ZenOptions>>();
        _optionsMock.Setup(x => x.Value).Returns(_options);

        _promptCacheMock = new Mock<IPromptCache>();
        _loggerMock = new Mock<ILogger<ZenProvider>>();
        _desktopContextServiceMock = new Mock<IDesktopContextService>();
        _queryProcessorMock = new Mock<IQueryProcessor>();
    }

    private ZenProvider CreateSut()
    {
        var httpClient = new HttpClient();
        return new ZenProvider(
            httpClient,
            _optionsMock.Object,
            _promptCacheMock.Object,
            _loggerMock.Object,
            _desktopContextServiceMock.Object,
            _queryProcessorMock.Object);
    }

    [Fact]
    public void ProviderName_ReturnsZen()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var name = sut.ProviderName;

        // Assert
        Assert.Equal("zen", name);
    }

    [Fact]
    public void ModelName_ReturnsConfiguredModel()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var modelName = sut.ModelName;

        // Assert
        Assert.Equal("alpha-glm-4.7", modelName);
    }

    [Fact]
    public async Task CorrectTextAsync_WhenDisabledInConfig_ReturnsOriginalText()
    {
        // Arrange
        _options.Enabled = false;
        var sut = CreateSut();
        var inputText = "This is a test transcription that needs correction.";

        // Act
        var result = await sut.CorrectTextAsync(inputText);

        // Assert
        Assert.Equal(inputText, result.CorrectedText);
        Assert.Null(result.PromptId);
        Assert.Equal(0, result.DurationMs);
    }

    [Fact]
    public async Task CorrectTextAsync_WhenDisabledAtRuntime_ReturnsOriginalText()
    {
        // Arrange
        var sut = CreateSut();
        sut.SetEnabled(false);
        var inputText = "This is a test transcription that needs correction.";

        // Act
        var result = await sut.CorrectTextAsync(inputText);

        // Assert
        Assert.Equal(inputText, result.CorrectedText);
        Assert.Null(result.PromptId);
        Assert.Equal(0, result.DurationMs);
    }

    [Fact]
    public async Task CorrectTextAsync_WhenTextTooShort_ReturnsOriginalText()
    {
        // Arrange
        var sut = CreateSut();
        var inputText = "Short text"; // Less than 21 chars

        // Act
        var result = await sut.CorrectTextAsync(inputText);

        // Assert
        Assert.Equal(inputText, result.CorrectedText);
        Assert.Null(result.PromptId);
        Assert.Equal(0, result.DurationMs);
    }

    [Fact]
    public void SetEnabled_UpdatesRuntimeState()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        Assert.True(sut.IsEnabled());
        sut.SetEnabled(false);
        Assert.False(sut.IsEnabled());
        sut.SetEnabled(true);
        Assert.True(sut.IsEnabled());
    }

    [Fact]
    public void ReloadPrompt_ClearsCache()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        sut.ReloadPrompt();

        // Assert
        _promptCacheMock.Verify(x => x.ClearCache(), Times.Once);
    }

    [Fact]
    public void GetLastRateLimitHeaders_ReturnsEmptyDictionaryInitially()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var headers = sut.GetLastRateLimitHeaders();

        // Assert
        Assert.NotNull(headers);
        Assert.Empty(headers);
    }

    [Fact]
    public void Constructor_ThrowsOnNullPromptCache()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ZenProvider(
            new HttpClient(),
            _optionsMock.Object,
            null!,
            _loggerMock.Object,
            _desktopContextServiceMock.Object,
            _queryProcessorMock.Object));
    }

    [Fact]
    public void Constructor_ThrowsOnNullDesktopContextService()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ZenProvider(
            new HttpClient(),
            _optionsMock.Object,
            _promptCacheMock.Object,
            _loggerMock.Object,
            null!,
            _queryProcessorMock.Object));
    }

    [Fact]
    public void Constructor_ThrowsOnNullQueryProcessor()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ZenProvider(
            new HttpClient(),
            _optionsMock.Object,
            _promptCacheMock.Object,
            _loggerMock.Object,
            _desktopContextServiceMock.Object,
            null!));
    }
}
