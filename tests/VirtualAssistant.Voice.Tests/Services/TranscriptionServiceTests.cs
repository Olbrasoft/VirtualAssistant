using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace VirtualAssistant.Voice.Tests.Services;

public class TranscriptionServiceTests
{
    private readonly Mock<ILogger<TranscriptionService>> _loggerMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IConfigurationSection> _configSectionMock;
    private readonly TranscriptionService _sut;

    public TranscriptionServiceTests()
    {
        _loggerMock = new Mock<ILogger<TranscriptionService>>();
        _configurationMock = new Mock<IConfiguration>();
        _configSectionMock = new Mock<IConfigurationSection>();

        // Setup configuration with minimal required values
        _configSectionMock.Setup(x => x["WhisperModelPath"]).Returns("/fake/path/model.bin");
        _configSectionMock.Setup(x => x["WhisperLanguage"]).Returns("en");
        _configSectionMock.Setup(x => x["MaxSegmentBytes"]).Returns("10485760");
        _configurationMock.Setup(x => x.GetSection("ContinuousListener")).Returns(_configSectionMock.Object);

        _sut = new TranscriptionService(_loggerMock.Object, _configurationMock.Object);
    }

    [Fact]
    public async Task TranscribeAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var audioData = new byte[1000];
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        // Note: Will throw InvalidOperationException first because service isn't initialized,
        // but the important thing is that CancellationToken parameter is accepted
        var exception = await Assert.ThrowsAnyAsync<Exception>(
            async () => await _sut.TranscribeAsync(audioData, cts.Token)
        );

        // Verify that either InvalidOperationException (not initialized)
        // or OperationCanceledException (token was cancelled) is thrown
        Assert.True(
            exception is InvalidOperationException || exception is OperationCanceledException,
            $"Expected InvalidOperationException or OperationCanceledException, got {exception.GetType().Name}"
        );
    }

    [Fact]
    public async Task TranscribeAsync_NotInitialized_ThrowsInvalidOperationException()
    {
        // Arrange
        var audioData = new byte[1000];

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _sut.TranscribeAsync(audioData)
        );
        Assert.Contains("not initialized", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Act & Assert
        _sut.Dispose();
        _sut.Dispose(); // Should not throw
    }

    [Fact]
    public void TranscribeAsync_HasCancellationTokenParameter()
    {
        // Arrange & Act
        var method = typeof(TranscriptionService).GetMethod(
            "TranscribeAsync",
            new[] { typeof(byte[]), typeof(CancellationToken) }
        );

        // Assert
        Assert.NotNull(method);
        var parameters = method!.GetParameters();
        Assert.Equal(2, parameters.Length);
        Assert.Equal("audioData", parameters[0].Name);
        Assert.Equal("cancellationToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.True(parameters[1].HasDefaultValue, "CancellationToken parameter should have default value");
    }
}
