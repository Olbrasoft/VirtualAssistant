using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.LlmChain.Configuration;

namespace Olbrasoft.VirtualAssistant.LlmChain.Tests;

/// <summary>
/// Unit tests for LlmRequestBuilder - HTTP request building for LLM APIs.
/// </summary>
public class LlmRequestBuilderTests
{
    private readonly Mock<ILogger<LlmRequestBuilder>> _loggerMock;
    private readonly LlmRequestBuilder _sut;

    public LlmRequestBuilderTests()
    {
        _loggerMock = new Mock<ILogger<LlmRequestBuilder>>();
        _sut = new LlmRequestBuilder(_loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new LlmRequestBuilder(null!));
    }

    #endregion

    #region BuildRequest Tests

    [Fact]
    public void BuildRequest_ReturnsHttpRequestMessage_WithCorrectMethod()
    {
        // Arrange
        var request = CreateTestRequest();
        var provider = CreateTestProvider();

        // Act
        var result = _sut.BuildRequest(request, provider, "test-api-key");

        // Assert
        result.Should().NotBeNull();
        result.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public void BuildRequest_SetsCorrectUri()
    {
        // Arrange
        var request = CreateTestRequest();
        var provider = CreateTestProvider("https://api.example.com/v1/");

        // Act
        var result = _sut.BuildRequest(request, provider, "test-api-key");

        // Assert
        result.RequestUri.Should().NotBeNull();
        result.RequestUri!.ToString().Should().Be("https://api.example.com/v1/chat/completions");
    }

    [Fact]
    public void BuildRequest_SetsAuthorizationHeader()
    {
        // Arrange
        var request = CreateTestRequest();
        var provider = CreateTestProvider();
        var apiKey = "sk-test-key-12345";

        // Act
        var result = _sut.BuildRequest(request, provider, apiKey);

        // Assert
        result.Headers.Authorization.Should().NotBeNull();
        result.Headers.Authorization!.Scheme.Should().Be("Bearer");
        result.Headers.Authorization.Parameter.Should().Be(apiKey);
    }

    [Fact]
    public void BuildRequest_SetsContentTypeHeader()
    {
        // Arrange
        var request = CreateTestRequest();
        var provider = CreateTestProvider();

        // Act
        var result = _sut.BuildRequest(request, provider, "test-key");

        // Assert
        result.Content.Should().NotBeNull();
        result.Content!.Headers.ContentType.Should().NotBeNull();
        result.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task BuildRequest_ContentContainsCorrectModel()
    {
        // Arrange
        var request = CreateTestRequest();
        var provider = CreateTestProvider();
        provider.Model = "gpt-4-turbo";

        // Act
        var result = _sut.BuildRequest(request, provider, "test-key");

        // Assert
        var content = await result.Content!.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(content);
        json.GetProperty("model").GetString().Should().Be("gpt-4-turbo");
    }

    [Fact]
    public async Task BuildRequest_ContentContainsSystemMessage()
    {
        // Arrange
        var request = CreateTestRequest(systemPrompt: "You are a helpful assistant.");
        var provider = CreateTestProvider();

        // Act
        var result = _sut.BuildRequest(request, provider, "test-key");

        // Assert
        var content = await result.Content!.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(content);
        var messages = json.GetProperty("messages");
        messages[0].GetProperty("role").GetString().Should().Be("system");
        messages[0].GetProperty("content").GetString().Should().Be("You are a helpful assistant.");
    }

    [Fact]
    public async Task BuildRequest_ContentContainsUserMessage()
    {
        // Arrange
        var request = CreateTestRequest(userMessage: "Hello, how are you?");
        var provider = CreateTestProvider();

        // Act
        var result = _sut.BuildRequest(request, provider, "test-key");

        // Assert
        var content = await result.Content!.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(content);
        var messages = json.GetProperty("messages");
        messages[1].GetProperty("role").GetString().Should().Be("user");
        messages[1].GetProperty("content").GetString().Should().Be("Hello, how are you?");
    }

    [Fact]
    public async Task BuildRequest_ContentContainsTemperature()
    {
        // Arrange
        var request = CreateTestRequest(temperature: 0.7f);
        var provider = CreateTestProvider();

        // Act
        var result = _sut.BuildRequest(request, provider, "test-key");

        // Assert
        var content = await result.Content!.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(content);
        json.GetProperty("temperature").GetSingle().Should().BeApproximately(0.7f, 0.001f);
    }

    [Fact]
    public async Task BuildRequest_ContentContainsMaxTokens()
    {
        // Arrange
        var request = CreateTestRequest(maxTokens: 1000);
        var provider = CreateTestProvider();

        // Act
        var result = _sut.BuildRequest(request, provider, "test-key");

        // Assert
        var content = await result.Content!.ReadAsStringAsync();
        var json = JsonSerializer.Deserialize<JsonElement>(content);
        json.GetProperty("max_tokens").GetInt32().Should().Be(1000);
    }

    [Fact]
    public void BuildRequest_ReturnsDisposableMessage()
    {
        // Arrange
        var request = CreateTestRequest();
        var provider = CreateTestProvider();

        // Act
        var result = _sut.BuildRequest(request, provider, "test-key");

        // Assert - Should be disposable without throwing
        result.Dispose();
    }

    [Fact]
    public void BuildRequest_DoesNotModifySharedHttpClient()
    {
        // Arrange
        var request = CreateTestRequest();
        var provider = CreateTestProvider();

        // Act - Build multiple requests
        var result1 = _sut.BuildRequest(request, provider, "key1");
        var result2 = _sut.BuildRequest(request, provider, "key2");

        // Assert - Each request should have its own authorization header
        result1.Headers.Authorization!.Parameter.Should().Be("key1");
        result2.Headers.Authorization!.Parameter.Should().Be("key2");

        result1.Dispose();
        result2.Dispose();
    }

    #endregion

    #region Helper Methods

    private static LlmChainRequest CreateTestRequest(
        string systemPrompt = "Test system prompt",
        string userMessage = "Test user message",
        float temperature = 0.5f,
        int maxTokens = 500) => new()
    {
        SystemPrompt = systemPrompt,
        UserMessage = userMessage,
        Temperature = temperature,
        MaxTokens = maxTokens
    };

    private static LlmProviderConfig CreateTestProvider(string baseUrl = "https://api.test.com/") => new()
    {
        Name = "TestProvider",
        BaseUrl = baseUrl,
        Model = "test-model",
        ApiKeys = ["test-key"],
        Enabled = true
    };

    #endregion
}
