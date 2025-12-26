using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Enums;
using Olbrasoft.VirtualAssistant.Core.Exceptions;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace VirtualAssistant.Voice.Tests.Services;

/// <summary>
/// Unit tests for MultiProviderLlmRouter round-robin routing and rate limit handling.
/// </summary>
public class MultiProviderLlmRouterTests
{
    private readonly Mock<ILogger<MultiProviderLlmRouter>> _loggerMock;
    private readonly List<TestLlmRouterService> _mockRouters;
    private readonly MultiProviderLlmRouter _sut;

    public MultiProviderLlmRouterTests()
    {
        _loggerMock = new Mock<ILogger<MultiProviderLlmRouter>>();

        // Create test router services for all providers
        _mockRouters = new List<TestLlmRouterService>
        {
            new TestLlmRouterService(LlmProvider.Mistral, "Mistral"),
            new TestLlmRouterService(LlmProvider.Groq, "Groq"),
            new TestLlmRouterService(LlmProvider.Cerebras, "Cerebras")
        };

        _sut = new MultiProviderLlmRouter(_loggerMock.Object, _mockRouters);
    }

    [Fact]
    public async Task RouteAsync_WithEmptyInput_ReturnsIgnored()
    {
        // Arrange
        var emptyInput = "   ";

        // Act
        var result = await _sut.RouteAsync(emptyInput);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(LlmRouterAction.Ignore, result.Action);
        Assert.Equal("Empty input", result.Reason);
    }

    [Fact]
    public async Task RouteAsync_WhenFirstProviderSucceeds_ReturnsResult()
    {
        // Arrange
        var input = "test input";
        var expectedResult = new LlmRouterResult
        {
            Success = true,
            Action = LlmRouterAction.Respond,
            Response = "Test response",
            ResponseTimeMs = 100
        };

        _mockRouters[0].SetupResult(expectedResult);

        // Act
        var result = await _sut.RouteAsync(input);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(LlmRouterAction.Respond, result.Action);
        Assert.Equal("Test response", result.Response);
        Assert.Equal("Mistral", result.ProviderName);
    }

    [Fact]
    public async Task RouteAsync_WhenFirstProviderFails_FallsBackToSecond()
    {
        // Arrange
        var input = "test input";
        var failureResult = LlmRouterResult.Error("Provider failed", 50);
        var successResult = new LlmRouterResult
        {
            Success = true,
            Action = LlmRouterAction.Respond,
            Response = "Fallback response",
            ResponseTimeMs = 100
        };

        _mockRouters[0].SetupResult(failureResult);
        _mockRouters[1].SetupResult(successResult);

        // Act
        var result = await _sut.RouteAsync(input);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(LlmRouterAction.Respond, result.Action);
        Assert.Equal("Fallback response", result.Response);
        Assert.Equal("Groq", result.ProviderName);
    }

    [Fact]
    public async Task RouteAsync_WhenProviderThrowsRateLimitException_MarksProviderRateLimited()
    {
        // Arrange
        var input = "test input";
        var resetAt = DateTime.UtcNow.AddMinutes(5);
        var rateLimitException = new RateLimitException(LlmProvider.Mistral, "Rate limited", resetAt);
        var successResult = new LlmRouterResult
        {
            Success = true,
            Action = LlmRouterAction.Respond,
            Response = "Fallback response",
            ResponseTimeMs = 100
        };

        _mockRouters[0].SetupException(rateLimitException);
        _mockRouters[1].SetupResult(successResult);

        // Act
        var result = await _sut.RouteAsync(input);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Groq", result.ProviderName);

        // Verify that on second call, Mistral is skipped (still rate limited)
        _mockRouters[1].Reset();
        _mockRouters[1].SetupResult(successResult);

        var result2 = await _sut.RouteAsync(input);
        Assert.Equal("Groq", result2.ProviderName);
    }

    [Fact]
    public async Task RouteAsync_WhenAllProvidersRateLimited_ReturnsError()
    {
        // Arrange
        var input = "test input";
        var resetAt = DateTime.UtcNow.AddMinutes(5);

        foreach (var router in _mockRouters)
        {
            router.SetupException(new RateLimitException(router.Provider, "Rate limited", resetAt));
        }

        // Act
        var result = await _sut.RouteAsync(input);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("All providers failed", result.ErrorMessage);
    }

    [Fact]
    public async Task RouteAsync_UsesRoundRobinRotation()
    {
        // Arrange
        var input = "test input";
        var successResult = new LlmRouterResult
        {
            Success = true,
            Action = LlmRouterAction.Respond,
            Response = "Response",
            ResponseTimeMs = 100
        };

        foreach (var router in _mockRouters)
        {
            router.SetupResult(successResult);
        }

        // Act - Make 6 calls to verify round-robin (2 full cycles)
        var results = new List<string?>();
        for (int i = 0; i < 6; i++)
        {
            var result = await _sut.RouteAsync(input);
            results.Add(result.ProviderName);

            // Reset for next call
            foreach (var router in _mockRouters)
            {
                router.Reset();
                router.SetupResult(successResult);
            }
        }

        // Assert - Should cycle: Mistral, Groq, Cerebras, Mistral, Groq, Cerebras
        Assert.Equal("Mistral", results[0]);
        Assert.Equal("Groq", results[1]);
        Assert.Equal("Cerebras", results[2]);
        Assert.Equal("Mistral", results[3]);
        Assert.Equal("Groq", results[4]);
        Assert.Equal("Cerebras", results[5]);
    }

    [Fact]
    public async Task RouteAsync_SkipsRateLimitedProviders()
    {
        // Arrange
        var input = "test input";
        var resetAt = DateTime.UtcNow.AddMinutes(5);
        var successResult = new LlmRouterResult
        {
            Success = true,
            Action = LlmRouterAction.Respond,
            Response = "Response",
            ResponseTimeMs = 100
        };

        // Rate limit Mistral (first in round-robin)
        _mockRouters[0].SetupException(new RateLimitException(LlmProvider.Mistral, "Rate limited", resetAt));
        _mockRouters[1].SetupResult(successResult);
        _mockRouters[2].SetupResult(successResult);

        // Act - First call should skip Mistral and use Groq
        var result1 = await _sut.RouteAsync(input);

        // Reset for second call
        foreach (var router in _mockRouters)
        {
            router.Reset();
            router.SetupResult(successResult);
        }

        // Second call should skip Mistral and use Cerebras
        var result2 = await _sut.RouteAsync(input);

        // Assert
        Assert.Equal("Groq", result1.ProviderName);
        Assert.Equal("Cerebras", result2.ProviderName);
    }

    [Fact]
    public async Task RouteAsync_CleansUpExpiredRateLimits()
    {
        // Arrange
        var input = "test input";
        var expiredResetAt = DateTime.UtcNow.AddSeconds(-1); // Already expired
        var successResult = new LlmRouterResult
        {
            Success = true,
            Action = LlmRouterAction.Respond,
            Response = "Response",
            ResponseTimeMs = 100
        };

        // Rate limit Mistral with expired time
        _mockRouters[0].SetupException(new RateLimitException(LlmProvider.Mistral, "Rate limited", expiredResetAt));
        _mockRouters[1].SetupResult(successResult);

        // Act - First call triggers rate limit, falls back to Groq
        var result1 = await _sut.RouteAsync(input);
        Assert.Equal("Groq", result1.ProviderName);

        // Reset all routers
        foreach (var router in _mockRouters)
        {
            router.Reset();
            router.SetupResult(successResult);
        }

        // Small delay to ensure expiration
        await Task.Delay(100);

        // Second call continues round-robin (Groq -> Cerebras)
        var result2 = await _sut.RouteAsync(input);
        Assert.Equal("Cerebras", result2.ProviderName);

        // Reset all routers again
        foreach (var router in _mockRouters)
        {
            router.Reset();
            router.SetupResult(successResult);
        }

        // Third call completes the cycle and should now use Mistral again
        // (rate limit expired and cleaned up)
        var result3 = await _sut.RouteAsync(input);

        // Assert - Mistral should be available again (rate limit expired and cleaned up)
        Assert.Equal("Mistral", result3.ProviderName);
    }

    [Fact]
    public async Task RouteAsync_WhenProviderThrowsException_TriesNextProvider()
    {
        // Arrange
        var input = "test input";
        var successResult = new LlmRouterResult
        {
            Success = true,
            Action = LlmRouterAction.Respond,
            Response = "Response",
            ResponseTimeMs = 100
        };

        _mockRouters[0].SetupException(new InvalidOperationException("Provider error"));
        _mockRouters[1].SetupResult(successResult);

        // Act
        var result = await _sut.RouteAsync(input);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Groq", result.ProviderName);
    }

    [Fact]
    public async Task RouteAsync_WhenAllProvidersFail_ReturnsError()
    {
        // Arrange
        var input = "test input";
        var failureResult = LlmRouterResult.Error("Provider failed", 50);

        foreach (var router in _mockRouters)
        {
            router.SetupResult(failureResult);
        }

        // Act
        var result = await _sut.RouteAsync(input);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("All providers failed", result.ErrorMessage);
        Assert.Contains("Provider failed", result.ErrorMessage);
    }

    /// <summary>
    /// Test double for BaseLlmRouterService that allows controlled behavior.
    /// </summary>
    private class TestLlmRouterService : BaseLlmRouterService
    {
        private LlmRouterResult? _resultToReturn;
        private Exception? _exceptionToThrow;

        public override string ProviderName { get; }
        public override LlmProvider Provider { get; }

        public TestLlmRouterService(LlmProvider provider, string providerName)
            : base(Mock.Of<ILogger>(), new HttpClient(), "test-model", Mock.Of<IPromptLoader>())
        {
            Provider = provider;
            ProviderName = providerName;
        }

        public void SetupResult(LlmRouterResult result)
        {
            _resultToReturn = result;
            _exceptionToThrow = null;
        }

        public void SetupException(Exception exception)
        {
            _exceptionToThrow = exception;
            _resultToReturn = null;
        }

        public void Reset()
        {
            _resultToReturn = null;
            _exceptionToThrow = null;
        }

        public override Task<LlmRouterResult> RouteAsync(string inputText, bool isDiscussionActive = false, CancellationToken cancellationToken = default)
        {
            if (_exceptionToThrow != null)
            {
                throw _exceptionToThrow;
            }

            return Task.FromResult(_resultToReturn ?? LlmRouterResult.Error("Not configured", 0));
        }
    }
}
