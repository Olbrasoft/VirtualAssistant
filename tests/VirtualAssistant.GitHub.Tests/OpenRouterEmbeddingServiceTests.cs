using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using VirtualAssistant.GitHub.Configuration;
using VirtualAssistant.GitHub.Services;

namespace VirtualAssistant.GitHub.Tests;

public class OpenRouterEmbeddingServiceTests
{
    private readonly Mock<ILogger<OpenRouterEmbeddingService>> _loggerMock;

    public OpenRouterEmbeddingServiceTests()
    {
        _loggerMock = new Mock<ILogger<OpenRouterEmbeddingService>>();
    }

    private OpenRouterEmbeddingService CreateService(
        EmbeddingSettings? settings = null,
        HttpMessageHandler? handler = null)
    {
        settings ??= new EmbeddingSettings
        {
            ApiKey = "test-api-key",
            Model = "openai/text-embedding-3-small",
            BaseUrl = "https://openrouter.ai/api/v1",
            MinContentLength = 10
        };

        var options = Options.Create(settings);
        var httpClient = handler != null
            ? new HttpClient(handler)
            : new HttpClient();

        return new OpenRouterEmbeddingService(httpClient, options, _loggerMock.Object);
    }

    [Fact]
    public void Constructor_WithApiKey_SetsIsConfiguredTrue()
    {
        // Arrange & Act
        var service = CreateService();

        // Assert
        Assert.True(service.IsConfigured);
    }

    [Fact]
    public void Constructor_WithoutApiKey_SetsIsConfiguredFalse()
    {
        // Arrange
        var settings = new EmbeddingSettings { ApiKey = "" };

        // Act
        var service = CreateService(settings);

        // Assert
        Assert.False(service.IsConfigured);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WhenNotConfigured_ReturnsNull()
    {
        // Arrange
        var settings = new EmbeddingSettings { ApiKey = "" };
        var service = CreateService(settings);

        // Act
        var result = await service.GenerateEmbeddingAsync("test text");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithNullText_ReturnsNull()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GenerateEmbeddingAsync(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithEmptyText_ReturnsNull()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GenerateEmbeddingAsync("");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithShortText_ReturnsNull()
    {
        // Arrange
        var settings = new EmbeddingSettings
        {
            ApiKey = "test-key",
            SkipShortContent = true,
            MinContentLength = 10
        };
        var service = CreateService(settings);

        // Act
        var result = await service.GenerateEmbeddingAsync("short"); // 5 chars < 10

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithShortTextAndSkipDisabled_CallsApi()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var responseContent = JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new { embedding = new float[] { 0.1f, 0.2f, 0.3f }, index = 0 }
            }
        });

        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var settings = new EmbeddingSettings
        {
            ApiKey = "test-key",
            SkipShortContent = false, // Don't skip short content
            MinContentLength = 10,
            BaseUrl = "https://openrouter.ai/api/v1"
        };
        var service = CreateService(settings, mockHandler.Object);

        // Act
        var result = await service.GenerateEmbeddingAsync("short");

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GenerateEmbeddingsBatchAsync_WhenNotConfigured_ReturnsAllNull()
    {
        // Arrange
        var settings = new EmbeddingSettings { ApiKey = "" };
        var service = CreateService(settings);
        var texts = new List<string?> { "text1", "text2", "text3" };

        // Act
        var result = await service.GenerateEmbeddingsBatchAsync(texts);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.All(result.Values, v => Assert.Null(v));
    }

    [Fact]
    public async Task GenerateEmbeddingsBatchAsync_WithEmptyList_ReturnsEmptyDictionary()
    {
        // Arrange
        var service = CreateService();
        var texts = new List<string?>();

        // Act
        var result = await service.GenerateEmbeddingsBatchAsync(texts);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GenerateEmbeddingsBatchAsync_SkipsNullAndShortTexts()
    {
        // Arrange
        var service = CreateService();
        var texts = new List<string?> { null, "short", "", "  " };

        // Act
        var result = await service.GenerateEmbeddingsBatchAsync(texts);

        // Assert
        Assert.Equal(4, result.Count);
        Assert.All(result.Values, v => Assert.Null(v));
    }
}
