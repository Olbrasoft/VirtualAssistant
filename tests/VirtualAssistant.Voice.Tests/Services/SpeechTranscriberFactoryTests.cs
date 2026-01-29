using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Data.Queries.ProviderQueries;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Tests.Services;

public class SpeechTranscriberFactoryTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IQueryProcessor> _queryProcessorMock;
    private readonly Mock<ILogger<SpeechTranscriberFactory>> _loggerMock;

    public SpeechTranscriberFactoryTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _queryProcessorMock = new Mock<IQueryProcessor>();
        _loggerMock = new Mock<ILogger<SpeechTranscriberFactory>>();
    }

    private SpeechTranscriberFactory CreateFactory(string primaryProvider = "whisper")
    {
        var settings = new SpeechProviderSettings
        {
            PrimaryProvider = primaryProvider,
            FallbackProvider = "whisper",
            EnableFallback = true
        };
        var options = Options.Create(settings);

        return new SpeechTranscriberFactory(
            _serviceProviderMock.Object,
            _queryProcessorMock.Object,
            options,
            _loggerMock.Object);
    }

    private void SetupProviderIds()
    {
        var providers = new List<Provider>
        {
            new() { Id = 13, Name = "Whisper Local", Type = "stt", Enabled = true },
            new() { Id = 14, Name = "Google Speech-to-Text", Type = "stt", Enabled = true }
        };

        _queryProcessorMock
            .Setup(qp => qp.ProcessAsync(
                It.IsAny<GetProvidersByTypeQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(providers);
    }

    [Fact]
    public void GetActiveProvider_WithUnavailablePrimary_ThrowsInvalidOperationException()
    {
        // Arrange - no transcriber registered
        var factory = CreateFactory("nonexistent");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => factory.GetActiveProvider());
        Assert.Contains("not available", exception.Message);
    }

    [Fact]
    public void GetProvider_WithUnknownProvider_ReturnsNull()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var provider = factory.GetProvider("unknown");

        // Assert
        Assert.Null(provider);
    }

    [Fact]
    public void GetAvailableProviders_ReturnsWhisperAndGoogle()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var providers = factory.GetAvailableProviders().ToList();

        // Assert
        Assert.Contains("whisper", providers);
        Assert.Contains("google", providers);
        Assert.Equal(2, providers.Count);
    }

    [Fact]
    public void GetProviderId_WithWhisper_ReturnsCorrectId()
    {
        // Arrange
        SetupProviderIds();
        var factory = CreateFactory();

        // Act
        var id = factory.GetProviderId("whisper");

        // Assert
        Assert.Equal(13, id);
    }

    [Fact]
    public void GetProviderId_WithGoogle_ReturnsCorrectId()
    {
        // Arrange
        SetupProviderIds();
        var factory = CreateFactory();

        // Act
        var id = factory.GetProviderId("google");

        // Assert
        Assert.Equal(14, id);
    }

    [Fact]
    public void GetProviderId_WithUnknownProvider_ThrowsArgumentException()
    {
        // Arrange
        SetupProviderIds();
        var factory = CreateFactory();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => factory.GetProviderId("unknown"));
        Assert.Contains("Unknown STT provider", exception.Message);
    }

    [Fact]
    public void GetProviderId_CachesProviderIds()
    {
        // Arrange
        SetupProviderIds();
        var factory = CreateFactory();

        // Act - call twice
        factory.GetProviderId("whisper");
        factory.GetProviderId("google");
        factory.GetProviderId("whisper");

        // Assert - query processor should be called only once
        _queryProcessorMock.Verify(
            qp => qp.ProcessAsync(
                It.IsAny<GetProvidersByTypeQuery>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void GetProviderId_QueriesOnlyStTypeProviders()
    {
        // Arrange
        SetupProviderIds();
        var factory = CreateFactory();

        // Act
        factory.GetProviderId("whisper");

        // Assert - query should be for "stt" type
        _queryProcessorMock.Verify(
            qp => qp.ProcessAsync(
                It.Is<GetProvidersByTypeQuery>(q => q.Type == "stt"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = Options.Create(new SpeechProviderSettings());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SpeechTranscriberFactory(
            null!,
            _queryProcessorMock.Object,
            settings,
            _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullQueryProcessor_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = Options.Create(new SpeechProviderSettings());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SpeechTranscriberFactory(
            _serviceProviderMock.Object,
            null!,
            settings,
            _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullSettings_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SpeechTranscriberFactory(
            _serviceProviderMock.Object,
            _queryProcessorMock.Object,
            null!,
            _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = Options.Create(new SpeechProviderSettings());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SpeechTranscriberFactory(
            _serviceProviderMock.Object,
            _queryProcessorMock.Object,
            settings,
            null!));
    }
}
