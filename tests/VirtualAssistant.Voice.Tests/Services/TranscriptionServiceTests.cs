using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Tests.Services;

public class TranscriptionServiceTests
{
    private readonly Mock<ILogger<TranscriptionService>> _loggerMock;
    private readonly Mock<ISpeechTranscriber> _transcriberMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IConfigurationSection> _configSectionMock;
    private readonly TranscriptionService _sut;

    public TranscriptionServiceTests()
    {
        _loggerMock = new Mock<ILogger<TranscriptionService>>();
        _transcriberMock = new Mock<ISpeechTranscriber>();
        _configurationMock = new Mock<IConfiguration>();
        _configSectionMock = new Mock<IConfigurationSection>();

        // Setup configuration with minimal required values
        _configSectionMock.Setup(x => x["MaxSegmentBytes"]).Returns("10485760");
        _configurationMock.Setup(x => x.GetSection("ContinuousListener")).Returns(_configSectionMock.Object);

        _sut = new TranscriptionService(_loggerMock.Object, _transcriberMock.Object, _configurationMock.Object);
    }

    [Fact]
    public async Task TranscribeAsync_WithCancelledToken_PassesToTranscriber()
    {
        // Arrange
        var audioData = new byte[1000];
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _transcriberMock
            .Setup(x => x.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await _sut.TranscribeAsync(audioData, cts.Token)
        );
    }

    [Fact]
    public async Task TranscribeAsync_DelegatesToTranscriber()
    {
        // Arrange
        var audioData = new byte[1000];
        var expectedResult = new TranscriptionResult("test transcription", 0.95f);

        _transcriberMock
            .Setup(x => x.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _sut.TranscribeAsync(audioData);

        // Assert
        Assert.Equal(expectedResult.Text, result.Text);
        _transcriberMock.Verify(x => x.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TranscribeAsync_SuccessfulTranscription_RaisesRawTranscriptionReadyEvent()
    {
        // Arrange
        var audioData = new byte[1000];
        var expectedText = "raw transcription";
        string? receivedText = null;

        _transcriberMock
            .Setup(x => x.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult(expectedText, 0.95f));

        _sut.RawTranscriptionReady += text => receivedText = text;

        // Act
        await _sut.TranscribeAsync(audioData);

        // Assert
        Assert.Equal(expectedText, receivedText);
    }

    [Fact]
    public async Task TranscribeAsync_FailedTranscription_DoesNotRaiseRawTranscriptionReadyEvent()
    {
        // Arrange
        var audioData = new byte[1000];
        var called = false;

        _transcriberMock
            .Setup(x => x.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult("error"));

        _sut.RawTranscriptionReady += _ => called = true;

        // Act
        await _sut.TranscribeAsync(audioData);

        // Assert
        Assert.False(called);
    }

    [Fact]
    public async Task TranscribeAsync_EmptyTranscription_DoesNotRaiseRawTranscriptionReadyEvent()
    {
        // Arrange
        var audioData = new byte[1000];
        var called = false;

        _transcriberMock
            .Setup(x => x.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult("", 0.0f));

        _sut.RawTranscriptionReady += _ => called = true;

        // Act
        await _sut.TranscribeAsync(audioData);

        // Assert
        Assert.False(called);
    }

    [Fact]
    public async Task TranscribeAsync_RawTranscriptionReadyHandlerThrows_DoesNotFailTranscription()
    {
        // Arrange
        var audioData = new byte[1000];
        var expectedText = "test transcription";

        _transcriberMock
            .Setup(x => x.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TranscriptionResult(expectedText, 0.95f));

        _sut.RawTranscriptionReady += _ => throw new InvalidOperationException("Handler error");

        // Act
        var result = await _sut.TranscribeAsync(audioData);

        // Assert - transcription should succeed despite handler exception
        Assert.Equal(expectedText, result.Text);
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
            [typeof(byte[]), typeof(CancellationToken)]
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
