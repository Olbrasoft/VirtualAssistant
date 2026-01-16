using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Tests.Services;

public class LlmProviderFactoryTests
{
    private readonly Mock<ILogger<LlmProviderFactory>> _loggerMock;
    private readonly Mock<ILlmProvider> _mistralProviderMock;
    private readonly Mock<ILlmProvider> _zenProviderMock;

    public LlmProviderFactoryTests()
    {
        _loggerMock = new Mock<ILogger<LlmProviderFactory>>();
        _mistralProviderMock = new Mock<ILlmProvider>();
        _mistralProviderMock.Setup(p => p.ProviderName).Returns("mistral");

        _zenProviderMock = new Mock<ILlmProvider>();
        _zenProviderMock.Setup(p => p.ProviderName).Returns("zen");
    }

    private LlmProviderFactory CreateFactory(string activeProvider = "mistral")
    {
        var options = Options.Create(new LlmProviderOptions { ActiveProvider = activeProvider });
        var providers = new[] { _mistralProviderMock.Object, _zenProviderMock.Object };
        return new LlmProviderFactory(providers, options, _loggerMock.Object);
    }

    [Fact]
    public void Constructor_WithValidActiveProvider_InitializesCorrectly()
    {
        // Arrange & Act
        var factory = CreateFactory("mistral");

        // Assert
        var activeProvider = factory.GetActiveProvider();
        Assert.Equal("mistral", activeProvider.ProviderName);
    }

    [Fact]
    public void Constructor_WithInvalidActiveProvider_FallsBackToFirstProvider()
    {
        // Arrange & Act
        var factory = CreateFactory("nonexistent");

        // Assert
        var activeProvider = factory.GetActiveProvider();
        Assert.NotNull(activeProvider);
        // First provider in dictionary order (mistral was added first)
        Assert.Equal("mistral", activeProvider.ProviderName);
    }

    [Fact]
    public void GetActiveProvider_ReturnsConfiguredProvider()
    {
        // Arrange
        var factory = CreateFactory("zen");

        // Act
        var provider = factory.GetActiveProvider();

        // Assert
        Assert.Equal("zen", provider.ProviderName);
    }

    [Fact]
    public void GetProvider_WithValidName_ReturnsProvider()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var provider = factory.GetProvider("zen");

        // Assert
        Assert.NotNull(provider);
        Assert.Equal("zen", provider!.ProviderName);
    }

    [Fact]
    public void GetProvider_WithInvalidName_ReturnsNull()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var provider = factory.GetProvider("nonexistent");

        // Assert
        Assert.Null(provider);
    }

    [Fact]
    public void GetProvider_IsCaseInsensitive()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var provider = factory.GetProvider("MISTRAL");

        // Assert
        Assert.NotNull(provider);
        Assert.Equal("mistral", provider!.ProviderName);
    }

    [Fact]
    public void GetAvailableProviders_ReturnsAllProviderNames()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var providers = factory.GetAvailableProviders();

        // Assert
        Assert.Equal(2, providers.Count);
        Assert.Contains("mistral", providers);
        Assert.Contains("zen", providers);
    }

    [Fact]
    public void SetActiveProvider_WithValidProvider_SwitchesProvider()
    {
        // Arrange
        var factory = CreateFactory("mistral");

        // Act
        var result = factory.SetActiveProvider("zen");
        var activeProvider = factory.GetActiveProvider();

        // Assert
        Assert.True(result);
        Assert.Equal("zen", activeProvider.ProviderName);
    }

    [Fact]
    public void SetActiveProvider_WithInvalidProvider_ReturnsFalseAndKeepsCurrentProvider()
    {
        // Arrange
        var factory = CreateFactory("mistral");

        // Act
        var result = factory.SetActiveProvider("nonexistent");
        var activeProvider = factory.GetActiveProvider();

        // Assert
        Assert.False(result);
        Assert.Equal("mistral", activeProvider.ProviderName);
    }

    [Fact]
    public void SetActiveProvider_IsCaseInsensitive()
    {
        // Arrange
        var factory = CreateFactory("mistral");

        // Act
        var result = factory.SetActiveProvider("ZEN");
        var activeProvider = factory.GetActiveProvider();

        // Assert
        Assert.True(result);
        Assert.Equal("zen", activeProvider.ProviderName);
    }

    [Fact]
    public void Constructor_WithNoProviders_ThrowsOnGetActiveProvider()
    {
        // Arrange
        var options = Options.Create(new LlmProviderOptions { ActiveProvider = "mistral" });
        var factory = new LlmProviderFactory([], options, _loggerMock.Object);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => factory.GetActiveProvider());
    }

    [Fact]
    public void Constructor_WithDuplicateProviderNames_ThrowsArgumentException()
    {
        // Arrange
        var duplicateProvider1 = new Mock<ILlmProvider>();
        duplicateProvider1.Setup(p => p.ProviderName).Returns("mistral");

        var duplicateProvider2 = new Mock<ILlmProvider>();
        duplicateProvider2.Setup(p => p.ProviderName).Returns("MISTRAL"); // Same name, different case

        var providers = new[] { duplicateProvider1.Object, duplicateProvider2.Object };
        var options = Options.Create(new LlmProviderOptions { ActiveProvider = "mistral" });

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            new LlmProviderFactory(providers, options, _loggerMock.Object));

        Assert.Contains("Duplicate LLM provider name", ex.Message);
        Assert.Contains("mistral", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetProvider_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = CreateFactory();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => factory.GetProvider(null!));
    }

    [Fact]
    public void GetProvider_WithEmpty_ThrowsArgumentException()
    {
        // Arrange
        var factory = CreateFactory();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => factory.GetProvider(string.Empty));
    }

    [Fact]
    public void SetActiveProvider_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = CreateFactory();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => factory.SetActiveProvider(null!));
    }

    [Fact]
    public void SetActiveProvider_WithEmpty_ThrowsArgumentException()
    {
        // Arrange
        var factory = CreateFactory();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => factory.SetActiveProvider(string.Empty));
    }
}
