using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Service.Services;
using VirtualAssistant.Data;
using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Service.Tests.Services;

/// <summary>
/// Unit tests for DictationPersistenceService.
/// Tests database persistence of Whisper transcriptions and LLM corrections.
/// </summary>
public class DictationPersistenceServiceTests
{
    private readonly Mock<ILogger<DictationPersistenceService>> _loggerMock;
    private readonly Mock<IWhisperTranscriptionRepository> _whisperRepoMock;
    private readonly Mock<ILlmCorrectionRepository> _llmRepoMock;
    private readonly DictationPersistenceService _sut;

    public DictationPersistenceServiceTests()
    {
        _loggerMock = new Mock<ILogger<DictationPersistenceService>>();
        _whisperRepoMock = new Mock<IWhisperTranscriptionRepository>();
        _llmRepoMock = new Mock<ILlmCorrectionRepository>();

        _sut = new DictationPersistenceService(
            _loggerMock.Object,
            _whisperRepoMock.Object,
            _llmRepoMock.Object);
    }

    #region SaveTranscriptionAsync - Success Cases

    [Fact]
    public async Task SaveTranscriptionAsync_WithoutLlmCorrection_SavesOnlyWhisperTranscription()
    {
        // Arrange
        var audioData = new byte[32000]; // 1 second of audio (16-bit mono @ 16kHz)
        var originalText = "Hello world";
        string? correctedText = null; // No LLM correction
        var expectedTranscription = new WhisperTranscription
        {
            Id = 123,
            TranscribedText = originalText,
            AudioDurationMs = 1000
        };

        _whisperRepoMock
            .Setup(x => x.SaveAsync(originalText, (int?)1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTranscription);

        // Act
        var result = await _sut.SaveTranscriptionAsync(
            audioData,
            originalText,
            correctedText,
            llmDurationMs: 0,
            CancellationToken.None);

        // Assert
        Assert.Equal(123, result);
        _whisperRepoMock.Verify(x => x.SaveAsync(originalText, 1000, It.IsAny<CancellationToken>()), Times.Once);
        _llmRepoMock.Verify(x => x.SaveAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveTranscriptionAsync_WithLlmCorrection_SavesBothTranscriptionAndCorrection()
    {
        // Arrange
        var audioData = new byte[32000]; // 1 second of audio
        var originalText = "helo world"; // Whisper output (typo)
        var correctedText = "Hello world"; // LLM corrected
        var llmDurationMs = 250;

        var expectedTranscription = new WhisperTranscription
        {
            Id = 456,
            TranscribedText = originalText,
            AudioDurationMs = 1000
        };

        var expectedCorrection = new LlmCorrection
        {
            Id = 789,
            WhisperTranscriptionId = 456,
            CorrectedText = correctedText,
            DurationMs = llmDurationMs
        };

        _whisperRepoMock
            .Setup(x => x.SaveAsync(originalText, (int?)1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTranscription);

        _llmRepoMock
            .Setup(x => x.SaveAsync(456, correctedText, llmDurationMs, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCorrection);

        // Act
        var result = await _sut.SaveTranscriptionAsync(
            audioData,
            originalText,
            correctedText,
            llmDurationMs,
            CancellationToken.None);

        // Assert
        Assert.Equal(456, result);
        _whisperRepoMock.Verify(x => x.SaveAsync(originalText, 1000, It.IsAny<CancellationToken>()), Times.Once);
        _llmRepoMock.Verify(x => x.SaveAsync(456, correctedText, llmDurationMs, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveTranscriptionAsync_WithIdenticalCorrectedText_DoesNotSaveLlmCorrection()
    {
        // Arrange - LLM didn't actually change the text
        var audioData = new byte[16000];
        var originalText = "Hello world";
        var correctedText = "Hello world"; // Same as original

        var expectedTranscription = new WhisperTranscription
        {
            Id = 111,
            TranscribedText = originalText,
            AudioDurationMs = 500
        };

        _whisperRepoMock
            .Setup(x => x.SaveAsync(originalText, (int?)500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTranscription);

        // Act
        var result = await _sut.SaveTranscriptionAsync(
            audioData,
            originalText,
            correctedText,
            llmDurationMs: 100,
            CancellationToken.None);

        // Assert
        Assert.Equal(111, result);
        _llmRepoMock.Verify(x => x.SaveAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region SaveTranscriptionAsync - Audio Duration Calculation

    [Theory]
    [InlineData(32000, 1000)]  // 1 second: 32000 bytes = 16000 samples @ 16kHz = 1000ms
    [InlineData(16000, 500)]   // 0.5 seconds
    [InlineData(64000, 2000)]  // 2 seconds
    [InlineData(8000, 250)]    // 0.25 seconds
    public async Task SaveTranscriptionAsync_CalculatesAudioDurationCorrectly(int audioBytes, int expectedDurationMs)
    {
        // Arrange
        var audioData = new byte[audioBytes];
        var text = "test";

        _whisperRepoMock
            .Setup(x => x.SaveAsync(text, It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhisperTranscription { Id = 1, TranscribedText = text, AudioDurationMs = expectedDurationMs });

        // Act
        await _sut.SaveTranscriptionAsync(
            audioData,
            text,
            null,
            llmDurationMs: 0,
            CancellationToken.None);

        // Assert
        _whisperRepoMock.Verify(
            x => x.SaveAsync(text, (int?)expectedDurationMs, It.IsAny<CancellationToken>()),
            Times.Once,
            $"Expected duration {expectedDurationMs}ms for {audioBytes} bytes");
    }

    #endregion

    #region SaveTranscriptionAsync - Error Handling

    [Fact]
    public async Task SaveTranscriptionAsync_WhenWhisperSaveFails_ReturnsNull()
    {
        // Arrange
        var audioData = new byte[16000];
        var text = "test";

        _whisperRepoMock
            .Setup(x => x.SaveAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _sut.SaveTranscriptionAsync(
            audioData,
            text,
            null,
            llmDurationMs: 0,
            CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveTranscriptionAsync_WhenLlmSaveFails_StillReturnsTranscriptionId()
    {
        // Arrange - Whisper save succeeds, but LLM correction save fails
        var audioData = new byte[16000];
        var originalText = "original";
        var correctedText = "corrected";

        _whisperRepoMock
            .Setup(x => x.SaveAsync(originalText, (int?)500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhisperTranscription { Id = 999, TranscribedText = originalText, AudioDurationMs = 500 });

        _llmRepoMock
            .Setup(x => x.SaveAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM save failed"));

        // Act
        var result = await _sut.SaveTranscriptionAsync(
            audioData,
            originalText,
            correctedText,
            llmDurationMs: 100,
            CancellationToken.None);

        // Assert
        // Service should catch the exception and return null (graceful degradation)
        Assert.Null(result);
    }

    #endregion
}
