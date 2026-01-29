using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Configuration;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Voice.Tests.Services;

public class FallbackSpeechTranscriberTests
{
    private readonly Mock<ISpeechTranscriber> _primaryMock;
    private readonly Mock<ISpeechTranscriber> _fallbackMock;
    private readonly Mock<ISpeechTranscriberFactory> _factoryMock;
    private readonly Mock<ILogger<FallbackSpeechTranscriber>> _loggerMock;

    private const int PrimaryProviderId = 14; // Google
    private const int FallbackProviderId = 13; // Whisper

    public FallbackSpeechTranscriberTests()
    {
        _primaryMock = new Mock<ISpeechTranscriber>();
        _fallbackMock = new Mock<ISpeechTranscriber>();
        _factoryMock = new Mock<ISpeechTranscriberFactory>();
        _loggerMock = new Mock<ILogger<FallbackSpeechTranscriber>>();

        _primaryMock.SetupGet(p => p.Language).Returns("cs-CZ");
        _fallbackMock.SetupGet(p => p.Language).Returns("cs");

        _factoryMock.Setup(f => f.GetProviderId("google")).Returns(PrimaryProviderId);
        _factoryMock.Setup(f => f.GetProviderId("whisper")).Returns(FallbackProviderId);
    }

    private FallbackSpeechTranscriber CreateTranscriber(bool enableFallback = true)
    {
        var settings = new SpeechProviderSettings
        {
            PrimaryProvider = "google",
            FallbackProvider = "whisper",
            EnableFallback = enableFallback
        };

        return new FallbackSpeechTranscriber(
            _primaryMock.Object,
            _fallbackMock.Object,
            _factoryMock.Object,
            _loggerMock.Object,
            settings);
    }

    [Fact]
    public async Task TranscribeAsync_PrimarySuccess_ReturnsPrimaryResult()
    {
        // Arrange
        var expectedResult = new TranscriptionResult("Hello world", 0.95f);
        _primaryMock
            .Setup(p => p.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var transcriber = CreateTranscriber();
        var audioData = new byte[] { 1, 2, 3 };

        // Act
        var result = await transcriber.TranscribeAsync(audioData);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Hello world", result.Text);
        Assert.Equal(PrimaryProviderId, transcriber.LastUsedProviderId);

        _primaryMock.Verify(p => p.TranscribeAsync(audioData, It.IsAny<CancellationToken>()), Times.Once);
        _fallbackMock.Verify(p => p.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TranscribeAsync_PrimaryReturnsError_FallbackUsed()
    {
        // Arrange
        var primaryError = new TranscriptionResult("API error");
        var fallbackResult = new TranscriptionResult("Fallback result", 0.85f);

        _primaryMock
            .Setup(p => p.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(primaryError);
        _fallbackMock
            .Setup(p => p.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fallbackResult);

        var transcriber = CreateTranscriber();
        var audioData = new byte[] { 1, 2, 3 };

        // Act
        var result = await transcriber.TranscribeAsync(audioData);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Fallback result", result.Text);
        Assert.Equal(FallbackProviderId, transcriber.LastUsedProviderId);

        _primaryMock.Verify(p => p.TranscribeAsync(audioData, It.IsAny<CancellationToken>()), Times.Once);
        _fallbackMock.Verify(p => p.TranscribeAsync(audioData, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TranscribeAsync_PrimaryThrowsHttpRequestException_FallbackUsed()
    {
        // Arrange
        _primaryMock
            .Setup(p => p.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var fallbackResult = new TranscriptionResult("Fallback result", 0.85f);
        _fallbackMock
            .Setup(p => p.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fallbackResult);

        var transcriber = CreateTranscriber();
        var audioData = new byte[] { 1, 2, 3 };

        // Act
        var result = await transcriber.TranscribeAsync(audioData);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Fallback result", result.Text);
        Assert.Equal(FallbackProviderId, transcriber.LastUsedProviderId);
    }

    [Fact]
    public async Task TranscribeAsync_PrimaryThrowsTaskCanceledException_FallbackUsed()
    {
        // Arrange - TaskCanceledException for timeout (not user cancellation)
        _primaryMock
            .Setup(p => p.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("Request timed out"));

        var fallbackResult = new TranscriptionResult("Fallback result", 0.85f);
        _fallbackMock
            .Setup(p => p.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fallbackResult);

        var transcriber = CreateTranscriber();
        var audioData = new byte[] { 1, 2, 3 };

        // Act
        var result = await transcriber.TranscribeAsync(audioData);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Fallback result", result.Text);
        Assert.Equal(FallbackProviderId, transcriber.LastUsedProviderId);
    }

    [Fact]
    public async Task TranscribeAsync_PrimaryThrowsTimeoutException_FallbackUsed()
    {
        // Arrange
        _primaryMock
            .Setup(p => p.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Operation timed out"));

        var fallbackResult = new TranscriptionResult("Fallback result", 0.85f);
        _fallbackMock
            .Setup(p => p.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fallbackResult);

        var transcriber = CreateTranscriber();
        var audioData = new byte[] { 1, 2, 3 };

        // Act
        var result = await transcriber.TranscribeAsync(audioData);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(FallbackProviderId, transcriber.LastUsedProviderId);
    }

    [Fact]
    public async Task TranscribeAsync_FallbackDisabled_NeverCallsFallback()
    {
        // Arrange
        var primaryError = new TranscriptionResult("API error");
        _primaryMock
            .Setup(p => p.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(primaryError);

        var transcriber = CreateTranscriber(enableFallback: false);
        var audioData = new byte[] { 1, 2, 3 };

        // Act
        var result = await transcriber.TranscribeAsync(audioData);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("API error", result.ErrorMessage);
        Assert.Equal(PrimaryProviderId, transcriber.LastUsedProviderId);

        _fallbackMock.Verify(p => p.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TranscribeAsync_FallbackDisabled_PrimaryException_Propagates()
    {
        // Arrange
        _primaryMock
            .Setup(p => p.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var transcriber = CreateTranscriber(enableFallback: false);
        var audioData = new byte[] { 1, 2, 3 };

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            transcriber.TranscribeAsync(audioData));

        _fallbackMock.Verify(p => p.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TranscribeAsync_Stream_Success()
    {
        // Arrange
        var expectedResult = new TranscriptionResult("Hello world", 0.95f);
        _primaryMock
            .Setup(p => p.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var transcriber = CreateTranscriber();
        using var audioStream = new MemoryStream(new byte[] { 1, 2, 3 });

        // Act
        var result = await transcriber.TranscribeAsync(audioStream);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Hello world", result.Text);
        Assert.Equal(PrimaryProviderId, transcriber.LastUsedProviderId);
    }

    [Fact]
    public async Task TranscribeAsync_Stream_FallbackOnError()
    {
        // Arrange
        _primaryMock
            .Setup(p => p.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var fallbackResult = new TranscriptionResult("Fallback", 0.8f);
        _fallbackMock
            .Setup(p => p.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fallbackResult);

        var transcriber = CreateTranscriber();
        using var audioStream = new MemoryStream(new byte[] { 1, 2, 3 });

        // Act
        var result = await transcriber.TranscribeAsync(audioStream);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(FallbackProviderId, transcriber.LastUsedProviderId);
    }

    [Fact]
    public void Language_ReturnsPrimaryLanguage()
    {
        // Arrange
        var transcriber = CreateTranscriber();

        // Act & Assert
        Assert.Equal("cs-CZ", transcriber.Language);
    }

    [Fact]
    public void Constructor_WithNullPrimary_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new SpeechProviderSettings();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new FallbackSpeechTranscriber(
            null!,
            _fallbackMock.Object,
            _factoryMock.Object,
            _loggerMock.Object,
            settings));
    }

    [Fact]
    public void Constructor_WithNullFallback_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new SpeechProviderSettings();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new FallbackSpeechTranscriber(
            _primaryMock.Object,
            null!,
            _factoryMock.Object,
            _loggerMock.Object,
            settings));
    }

    [Fact]
    public void Constructor_WithNullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new SpeechProviderSettings();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new FallbackSpeechTranscriber(
            _primaryMock.Object,
            _fallbackMock.Object,
            null!,
            _loggerMock.Object,
            settings));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var settings = new SpeechProviderSettings();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new FallbackSpeechTranscriber(
            _primaryMock.Object,
            _fallbackMock.Object,
            _factoryMock.Object,
            null!,
            settings));
    }

    [Fact]
    public void Constructor_WithNullSettings_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new FallbackSpeechTranscriber(
            _primaryMock.Object,
            _fallbackMock.Object,
            _factoryMock.Object,
            _loggerMock.Object,
            null!));
    }

    [Fact]
    public async Task TranscribeAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var transcriber = CreateTranscriber();
        transcriber.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            transcriber.TranscribeAsync(new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public async Task TranscribeAsync_Stream_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var transcriber = CreateTranscriber();
        transcriber.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            transcriber.TranscribeAsync(new MemoryStream()));
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var transcriber = CreateTranscriber();

        // Act - Should not throw
        transcriber.Dispose();
        transcriber.Dispose();
    }

    [Fact]
    public async Task TranscribeAsync_PrimaryThrowsUnhandledException_Propagates()
    {
        // Arrange - InvalidOperationException is not in ShouldFallback list
        _primaryMock
            .Setup(p => p.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Internal error"));

        var transcriber = CreateTranscriber();
        var audioData = new byte[] { 1, 2, 3 };

        // Act & Assert - Exception should propagate, not trigger fallback
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            transcriber.TranscribeAsync(audioData));

        _fallbackMock.Verify(p => p.TranscribeAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
