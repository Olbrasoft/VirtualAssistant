using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.VirtualAssistant.LlmChain.Configuration;

namespace Olbrasoft.VirtualAssistant.LlmChain.Tests;

/// <summary>
/// Unit tests for ApiKeyRotator - API key rotation and rate limiting.
/// </summary>
public class ApiKeyRotatorTests
{
    private readonly Mock<ILogger<ApiKeyRotator>> _loggerMock;

    public ApiKeyRotatorTests()
    {
        _loggerMock = new Mock<ILogger<ApiKeyRotator>>();
    }

    private ApiKeyRotator CreateSut(List<LlmProviderConfig>? providers = null)
    {
        var options = new LlmChainOptions
        {
            Providers = providers ?? []
        };

        return new ApiKeyRotator(
            _loggerMock.Object,
            Options.Create(options));
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(new LlmChainOptions());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ApiKeyRotator(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ApiKeyRotator(_loggerMock.Object, null!));
    }

    [Fact]
    public void Constructor_LoadsKeysFromEnabledProviders()
    {
        // Arrange
        var providers = new List<LlmProviderConfig>
        {
            new() { Name = "TestProvider", BaseUrl = "https://test.com", Model = "test", ApiKeys = ["key1", "key2"], Enabled = true },
            new() { Name = "DisabledProvider", BaseUrl = "https://disabled.com", Model = "test", ApiKeys = ["key3"], Enabled = false }
        };

        // Act
        var sut = CreateSut(providers);

        // Assert
        sut.GetKeyCount("TestProvider").Should().Be(2);
        sut.GetKeyCount("DisabledProvider").Should().Be(0); // Disabled provider not loaded
    }

    #endregion

    #region GetKeyCount Tests

    [Fact]
    public void GetKeyCount_WithUnknownProvider_ReturnsZero()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        sut.GetKeyCount("UnknownProvider").Should().Be(0);
    }

    [Fact]
    public void GetKeyCount_WithKnownProvider_ReturnsCorrectCount()
    {
        // Arrange
        var providers = new List<LlmProviderConfig>
        {
            new() { Name = "Provider1", BaseUrl = "https://test.com", Model = "test", ApiKeys = ["k1", "k2", "k3"], Enabled = true }
        };
        var sut = CreateSut(providers);

        // Act & Assert
        sut.GetKeyCount("Provider1").Should().Be(3);
    }

    #endregion

    #region GetNextAvailableKey Tests

    [Fact]
    public void GetNextAvailableKey_WithUnknownProvider_ReturnsNull()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var (key, index) = sut.GetNextAvailableKey("UnknownProvider");

        // Assert
        key.Should().BeNull();
        index.Should().Be(-1);
    }

    [Fact]
    public void GetNextAvailableKey_WithEmptyKeys_ReturnsNull()
    {
        // Arrange
        var providers = new List<LlmProviderConfig>
        {
            new() { Name = "EmptyProvider", BaseUrl = "https://test.com", Model = "test", ApiKeys = [], Enabled = true }
        };
        var sut = CreateSut(providers);

        // Act
        var (key, index) = sut.GetNextAvailableKey("EmptyProvider");

        // Assert
        key.Should().BeNull();
        index.Should().Be(-1);
    }

    [Fact]
    public void GetNextAvailableKey_ReturnsFirstKey_OnFirstCall()
    {
        // Arrange
        var providers = new List<LlmProviderConfig>
        {
            new() { Name = "Provider", BaseUrl = "https://test.com", Model = "test", ApiKeys = ["key0", "key1", "key2"], Enabled = true }
        };
        var sut = CreateSut(providers);

        // Act
        var (key, index) = sut.GetNextAvailableKey("Provider");

        // Assert
        key.Should().Be("key0");
        index.Should().Be(0);
    }

    [Fact]
    public void GetNextAvailableKey_RotatesKeys_RoundRobin()
    {
        // Arrange
        var providers = new List<LlmProviderConfig>
        {
            new() { Name = "Provider", BaseUrl = "https://test.com", Model = "test", ApiKeys = ["key0", "key1", "key2"], Enabled = true }
        };
        var sut = CreateSut(providers);

        // Act
        var result1 = sut.GetNextAvailableKey("Provider");
        var result2 = sut.GetNextAvailableKey("Provider");
        var result3 = sut.GetNextAvailableKey("Provider");
        var result4 = sut.GetNextAvailableKey("Provider"); // Wraps around

        // Assert
        result1.Key.Should().Be("key0");
        result2.Key.Should().Be("key1");
        result3.Key.Should().Be("key2");
        result4.Key.Should().Be("key0"); // Round-robin wrap
    }

    [Fact]
    public void GetNextAvailableKey_SkipsRateLimitedKeys()
    {
        // Arrange
        var providers = new List<LlmProviderConfig>
        {
            new() { Name = "Provider", BaseUrl = "https://test.com", Model = "test", ApiKeys = ["key0", "key1", "key2"], Enabled = true }
        };
        var sut = CreateSut(providers);

        // First call gets key0
        sut.GetNextAvailableKey("Provider");
        // Mark key1 as rate limited
        sut.MarkRateLimited("Provider", 1, DateTime.UtcNow.AddMinutes(5));

        // Act - should skip key1 and return key2
        var (key, index) = sut.GetNextAvailableKey("Provider");

        // Assert
        key.Should().Be("key2");
        index.Should().Be(2);
    }

    [Fact]
    public void GetNextAvailableKey_ReturnsNull_WhenAllKeysRateLimited()
    {
        // Arrange
        var providers = new List<LlmProviderConfig>
        {
            new() { Name = "Provider", BaseUrl = "https://test.com", Model = "test", ApiKeys = ["key0", "key1"], Enabled = true }
        };
        var sut = CreateSut(providers);

        // Mark all keys as rate limited
        sut.MarkRateLimited("Provider", 0, DateTime.UtcNow.AddMinutes(5));
        sut.MarkRateLimited("Provider", 1, DateTime.UtcNow.AddMinutes(5));

        // Act
        var (key, index) = sut.GetNextAvailableKey("Provider");

        // Assert
        key.Should().BeNull();
        index.Should().Be(-1);
    }

    #endregion

    #region MarkRateLimited Tests

    [Fact]
    public void MarkRateLimited_MarksKeyAsUnavailable()
    {
        // Arrange
        var providers = new List<LlmProviderConfig>
        {
            new() { Name = "Provider", BaseUrl = "https://test.com", Model = "test", ApiKeys = ["key0"], Enabled = true }
        };
        var sut = CreateSut(providers);

        // Act
        sut.MarkRateLimited("Provider", 0, DateTime.UtcNow.AddMinutes(5));

        // Assert
        sut.HasAvailableKey("Provider").Should().BeFalse();
    }

    #endregion

    #region HasAvailableKey Tests

    [Fact]
    public void HasAvailableKey_WithUnknownProvider_ReturnsFalse()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        sut.HasAvailableKey("UnknownProvider").Should().BeFalse();
    }

    [Fact]
    public void HasAvailableKey_WithAvailableKeys_ReturnsTrue()
    {
        // Arrange
        var providers = new List<LlmProviderConfig>
        {
            new() { Name = "Provider", BaseUrl = "https://test.com", Model = "test", ApiKeys = ["key0", "key1"], Enabled = true }
        };
        var sut = CreateSut(providers);

        // Act & Assert
        sut.HasAvailableKey("Provider").Should().BeTrue();
    }

    [Fact]
    public void HasAvailableKey_WithSomeKeysRateLimited_ReturnsTrue()
    {
        // Arrange
        var providers = new List<LlmProviderConfig>
        {
            new() { Name = "Provider", BaseUrl = "https://test.com", Model = "test", ApiKeys = ["key0", "key1"], Enabled = true }
        };
        var sut = CreateSut(providers);
        sut.MarkRateLimited("Provider", 0, DateTime.UtcNow.AddMinutes(5));

        // Act & Assert
        sut.HasAvailableKey("Provider").Should().BeTrue();
    }

    [Fact]
    public void HasAvailableKey_WithAllKeysRateLimited_ReturnsFalse()
    {
        // Arrange
        var providers = new List<LlmProviderConfig>
        {
            new() { Name = "Provider", BaseUrl = "https://test.com", Model = "test", ApiKeys = ["key0", "key1"], Enabled = true }
        };
        var sut = CreateSut(providers);
        sut.MarkRateLimited("Provider", 0, DateTime.UtcNow.AddMinutes(5));
        sut.MarkRateLimited("Provider", 1, DateTime.UtcNow.AddMinutes(5));

        // Act & Assert
        sut.HasAvailableKey("Provider").Should().BeFalse();
    }

    #endregion

    #region CleanupExpiredRateLimits Tests

    [Fact]
    public void CleanupExpiredRateLimits_RemovesExpiredLimits()
    {
        // Arrange
        var providers = new List<LlmProviderConfig>
        {
            new() { Name = "Provider", BaseUrl = "https://test.com", Model = "test", ApiKeys = ["key0", "key1"], Enabled = true }
        };
        var sut = CreateSut(providers);

        // Mark keys with past expiration
        sut.MarkRateLimited("Provider", 0, DateTime.UtcNow.AddMinutes(-1)); // Expired
        sut.MarkRateLimited("Provider", 1, DateTime.UtcNow.AddMinutes(5));  // Not expired

        // Act
        sut.CleanupExpiredRateLimits();

        // Assert
        // First key should be available now, second still rate limited
        var (key, index) = sut.GetNextAvailableKey("Provider");
        key.Should().Be("key0");
    }

    [Fact]
    public void CleanupExpiredRateLimits_KeepsNonExpiredLimits()
    {
        // Arrange
        var providers = new List<LlmProviderConfig>
        {
            new() { Name = "Provider", BaseUrl = "https://test.com", Model = "test", ApiKeys = ["key0"], Enabled = true }
        };
        var sut = CreateSut(providers);
        sut.MarkRateLimited("Provider", 0, DateTime.UtcNow.AddMinutes(5)); // Not expired

        // Act
        sut.CleanupExpiredRateLimits();

        // Assert
        sut.HasAvailableKey("Provider").Should().BeFalse();
    }

    #endregion

    #region MaskKey Tests

    [Fact]
    public void MaskKey_WithShortKey_ReturnsMasked()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        sut.MaskKey("12345678").Should().Be("****");
        sut.MaskKey("short").Should().Be("****");
    }

    [Fact]
    public void MaskKey_WithLongKey_ReturnsPartialMask()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var masked = sut.MaskKey("sk-abcdefghijklmnop");

        // Assert
        masked.Should().Be("sk-a...mnop");
    }

    #endregion
}
