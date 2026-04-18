using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;
using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Data.Queries.LlmModelQueries;
using Olbrasoft.VirtualAssistant.Data.Queries.PromptQueries;
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
    private readonly Mock<ICliAppDetector> _cliAppDetectorMock;
    private readonly Mock<ISystemPromptResolver> _promptResolverMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly ZenOptions _options;

    public ZenProviderTests()
    {
        _options = new ZenOptions
        {
            ApiKey = "test-api-key",
            BaseUrl = "https://opencode.ai/zen/v1",
            Model = "glm-4.7",
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
        _cliAppDetectorMock = new Mock<ICliAppDetector>();
        _promptResolverMock = new Mock<ISystemPromptResolver>();
        _promptResolverMock
            .Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(("default prompt text", 4));

        var scopeMock = new Mock<IServiceScope>();
        var spMock = new Mock<IServiceProvider>();
        spMock.Setup(sp => sp.GetService(typeof(IQueryProcessor))).Returns(_queryProcessorMock.Object);
        scopeMock.Setup(s => s.ServiceProvider).Returns(spMock.Object);
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);
    }

    private ZenProvider CreateSut()
    {
        var httpClient = new HttpClient();
        return new ZenProvider(
            httpClient,
            _optionsMock.Object,
            _promptCacheMock.Object,
            _loggerMock.Object,
            _queryProcessorMock.Object,
            _promptResolverMock.Object,
            _scopeFactoryMock.Object);
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
        Assert.Equal("glm-4.7", modelName);
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

    private (ZenProvider Sut, Mock<HttpMessageHandler> HandlerMock) CreateSutWithMockHttp()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(handlerMock.Object);

        var sut = new ZenProvider(
            httpClient,
            _optionsMock.Object,
            _promptCacheMock.Object,
            _loggerMock.Object,
            _queryProcessorMock.Object,
            _promptResolverMock.Object,
            _scopeFactoryMock.Object);

        return (sut, handlerMock);
    }

    private void SetupMocksForCorrection(Mock<HttpMessageHandler> handlerMock, string responseText = "Corrected text")
    {
        _desktopContextServiceMock
            .Setup(x => x.GetCurrentContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DesktopContext(0, 1, "test", "test", "test", DateTime.UtcNow));

        _cliAppDetectorMock
            .Setup(x => x.DetectCliAppAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CliAppDetectionResult?)null);

        _queryProcessorMock
            .Setup(x => x.ProcessAsync(It.IsAny<GetPromptByAppIdPatternQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Prompt { Id = 1, PromptFileName = "Test.md" });

        _promptCacheMock
            .Setup(x => x.GetPrompt("Test.md"))
            .Returns("System prompt");

        _queryProcessorMock
            .Setup(x => x.ProcessAsync(It.IsAny<GetLlmModelByIdentifierQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmModel { Id = 13, Name = "glm-4.7", ModelIdentifier = "glm-4.7" });

        var json = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = responseText } } }
        });

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
    }

    [Fact]
    public async Task CorrectTextAsync_WhenReasoningEffortSet_IncludesItInRequest()
    {
        // Arrange
        _options.ReasoningEffort = "none";
        var (sut, handlerMock) = CreateSutWithMockHttp();
        SetupMocksForCorrection(handlerMock);
        string? capturedBody = null;

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedBody = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = "Corrected" } } } }),
                    System.Text.Encoding.UTF8, "application/json")
            });

        // Act
        await sut.CorrectTextAsync("This is a test transcription that needs correction.");

        // Assert
        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody);
        Assert.True(doc.RootElement.TryGetProperty("reasoning_effort", out var reasoningEffort));
        Assert.Equal("none", reasoningEffort.GetString());
    }

    [Fact]
    public async Task CorrectTextAsync_WhenReasoningEffortNull_OmitsItFromRequest()
    {
        // Arrange
        _options.ReasoningEffort = null;
        var (sut, handlerMock) = CreateSutWithMockHttp();
        SetupMocksForCorrection(handlerMock);
        string? capturedBody = null;

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedBody = await req.Content!.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = "Corrected" } } } }),
                    System.Text.Encoding.UTF8, "application/json")
            });

        // Act
        await sut.CorrectTextAsync("This is a test transcription that needs correction.");

        // Assert
        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody);
        Assert.False(doc.RootElement.TryGetProperty("reasoning_effort", out _));
    }

    [Fact]
    public void Constructor_ThrowsOnNullPromptCache()
    {
        Assert.Throws<ArgumentNullException>(() => new ZenProvider(
            new HttpClient(),
            _optionsMock.Object,
            null!,
            _loggerMock.Object,
            _queryProcessorMock.Object,
            _promptResolverMock.Object,
            _scopeFactoryMock.Object));
    }

    [Fact]
    public void Constructor_ThrowsOnNullQueryProcessor()
    {
        Assert.Throws<ArgumentNullException>(() => new ZenProvider(
            new HttpClient(),
            _optionsMock.Object,
            _promptCacheMock.Object,
            _loggerMock.Object,
            null!,
            _promptResolverMock.Object,
            _scopeFactoryMock.Object));
    }

    [Fact]
    public void Constructor_ThrowsOnNullPromptResolver()
    {
        Assert.Throws<ArgumentNullException>(() => new ZenProvider(
            new HttpClient(),
            _optionsMock.Object,
            _promptCacheMock.Object,
            _loggerMock.Object,
            _queryProcessorMock.Object,
            null!,
            _scopeFactoryMock.Object));
    }

    [Fact]
    public void Constructor_ThrowsOnNullScopeFactory()
    {
        Assert.Throws<ArgumentNullException>(() => new ZenProvider(
            new HttpClient(),
            _optionsMock.Object,
            _promptCacheMock.Object,
            _loggerMock.Object,
            _queryProcessorMock.Object,
            _promptResolverMock.Object,
            null!));
    }
}
