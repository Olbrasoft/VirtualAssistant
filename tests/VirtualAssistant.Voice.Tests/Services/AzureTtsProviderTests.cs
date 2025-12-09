using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace VirtualAssistant.Voice.Tests.Services;

/// <summary>
/// Unit tests for AzureTtsProvider.
/// Note: GenerateAudioAsync requires integration tests due to Azure SDK dependency.
/// </summary>
public class AzureTtsProviderTests
{
    private readonly Mock<ILogger<AzureTtsProvider>> _loggerMock;
    private readonly Mock<IOptions<AzureTtsOptions>> _optionsMock;

    public AzureTtsProviderTests()
    {
        _loggerMock = new Mock<ILogger<AzureTtsProvider>>();
        _optionsMock = new Mock<IOptions<AzureTtsOptions>>();
    }

    [Fact]
    public void Name_ReturnsAzureTTS()
    {
        // Arrange
        _optionsMock.Setup(x => x.Value).Returns(new AzureTtsOptions());
        var sut = new AzureTtsProvider(_loggerMock.Object, _optionsMock.Object);

        // Act
        var result = sut.Name;

        // Assert
        Assert.Equal("AzureTTS", result);
    }

    [Fact]
    public void IsAvailable_WhenNotConfigured_ReturnsFalse()
    {
        // Arrange - empty subscription key
        _optionsMock.Setup(x => x.Value).Returns(new AzureTtsOptions
        {
            SubscriptionKey = "",
            Region = "westeurope"
        });
        var sut = new AzureTtsProvider(_loggerMock.Object, _optionsMock.Object);

        // Act
        var result = sut.IsAvailable;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Constructor_WithValidOptions_CreatesInstance()
    {
        // Arrange
        _optionsMock.Setup(x => x.Value).Returns(new AzureTtsOptions
        {
            SubscriptionKey = "test-key",
            Region = "westeurope",
            Voice = "cs-CZ-AntoninNeural"
        });

        // Act
        var provider = new AzureTtsProvider(_loggerMock.Object, _optionsMock.Object);

        // Assert
        Assert.NotNull(provider);
        Assert.Equal("AzureTTS", provider.Name);
    }

    [Fact]
    public void ITtsProvider_Interface_IsImplemented()
    {
        // Arrange
        _optionsMock.Setup(x => x.Value).Returns(new AzureTtsOptions());
        var sut = new AzureTtsProvider(_loggerMock.Object, _optionsMock.Object);

        // Assert
        Assert.IsAssignableFrom<ITtsProvider>(sut);
    }

    [Fact]
    public async Task GenerateAudioAsync_WhenNotConfigured_ReturnsNull()
    {
        // Arrange - provider not configured (no key)
        _optionsMock.Setup(x => x.Value).Returns(new AzureTtsOptions
        {
            SubscriptionKey = "",
            Region = "westeurope"
        });
        var sut = new AzureTtsProvider(_loggerMock.Object, _optionsMock.Object);
        var config = new VoiceConfig("cs-CZ-AntoninNeural", "+0%", "+0%", "+0Hz");

        // Act
        var result = await sut.GenerateAudioAsync("Test", config, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void DefaultOptions_HasCorrectDefaults()
    {
        // Arrange
        var options = new AzureTtsOptions();

        // Assert
        Assert.Equal("westeurope", options.Region);
        Assert.Equal("cs-CZ-AntoninNeural", options.Voice);
        Assert.Equal("Audio24Khz48KBitRateMonoMp3", options.OutputFormat);
        Assert.Equal(string.Empty, options.SubscriptionKey);
    }

    [Fact]
    public void SectionName_IsCorrect()
    {
        // Assert
        Assert.Equal("AzureTts", AzureTtsOptions.SectionName);
    }

    // Note: Integration tests for actual audio generation would require
    // valid Azure credentials and network access, which is beyond unit testing scope.
    // The Azure SDK functionality should be tested via integration tests.
}
