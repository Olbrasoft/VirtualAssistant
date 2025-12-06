using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using VirtualAssistant.GitHub.Configuration;
using VirtualAssistant.GitHub.Services;

namespace VirtualAssistant.GitHub.Tests;

public class OllamaEmbeddingServiceTests
{
    private readonly Mock<ILogger<OllamaEmbeddingService>> _loggerMock;

    public OllamaEmbeddingServiceTests()
    {
        _loggerMock = new Mock<ILogger<OllamaEmbeddingService>>();
    }

    private OllamaEmbeddingService CreateService(
        EmbeddingSettings? settings = null,
        HttpMessageHandler? handler = null)
    {
        settings ??= new EmbeddingSettings
        {
            Model = "nomic-embed-text",
            BaseUrl = "http://localhost:11434",
            MinContentLength = 10
        };

        var options = Options.Create(settings);
        var httpClient = handler != null
            ? new HttpClient(handler)
            : new HttpClient();

        return new OllamaEmbeddingService(httpClient, options, _loggerMock.Object);
    }

    [Fact]
    public void IsConfigured_AlwaysReturnsTrue()
    {
        // Arrange & Act
        var service = CreateService();

        // Assert - Ollama is always "configured" (runs locally)
        Assert.True(service.IsConfigured);
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
    public async Task GenerateEmbeddingAsync_WithValidText_ReturnsEmbedding()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        var embedding = new float[768];
        for (int i = 0; i < 768; i++) embedding[i] = 0.01f * i;

        var responseContent = JsonSerializer.Serialize(new { embedding });

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
            Model = "nomic-embed-text",
            BaseUrl = "http://localhost:11434",
            SkipShortContent = false
        };
        var service = CreateService(settings, mockHandler.Object);

        // Act
        var result = await service.GenerateEmbeddingAsync("This is a test text for embedding generation");

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithApiError_ReturnsNull()
    {
        // Arrange
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("Ollama error")
            });

        var settings = new EmbeddingSettings
        {
            SkipShortContent = false,
            BaseUrl = "http://localhost:11434"
        };
        var service = CreateService(settings, mockHandler.Object);

        // Act
        var result = await service.GenerateEmbeddingAsync("Test text");

        // Assert
        Assert.Null(result);
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
