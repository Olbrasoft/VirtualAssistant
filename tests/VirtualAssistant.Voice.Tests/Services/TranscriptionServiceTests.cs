using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace VirtualAssistant.Voice.Tests.Services;

public class TranscriptionServiceTests
{
    private readonly Mock<ILogger<TranscriptionService>> _loggerMock;
    private readonly Mock<ISpeechTranscriber> _transcriberMock;
    private readonly IOptions<ContinuousListenerOptions> _options;
    private readonly TranscriptionService _sut;

    public TranscriptionServiceTests()
    {
        _loggerMock = new Mock<ILogger<TranscriptionService>>();
        _transcriberMock = new Mock<ISpeechTranscriber>();

        // Create options with default values (MaxSegmentBytes is computed from SampleRate and MaxSegmentMs)
        _options = Options.Create(new ContinuousListenerOptions());

        _sut = new TranscriptionService(_loggerMock.Object, _transcriberMock.Object, _options);
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
