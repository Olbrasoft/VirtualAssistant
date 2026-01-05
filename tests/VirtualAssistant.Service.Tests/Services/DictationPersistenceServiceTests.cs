using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Olbrasoft.Data.Cqrs;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Service.Infrastructure;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Services;

/// <summary>
/// Unit tests for <see cref="DictationPersistenceService"/>.
/// Verifies correct database persistence of Whisper transcriptions and LLM corrections,
/// including error handling, input validation, and audio duration calculations.
/// </summary>
public class DictationPersistenceServiceTests
{
    private readonly Mock<ILogger<DictationPersistenceService>> _loggerMock;
    private readonly Mock<ICommandExecutor> _commandExecutorMock;
    private readonly IOptions<AudioRecordingOptions> _defaultOptions;
    private readonly DictationPersistenceService _service;

    public DictationPersistenceServiceTests()
    {
        _loggerMock = new Mock<ILogger<DictationPersistenceService>>();
        _commandExecutorMock = new Mock<ICommandExecutor>();
        _defaultOptions = Options.Create(new AudioRecordingOptions
        {
            SampleRate = 16000,
            BitsPerSample = 16,
            Channels = 1,
            MaxRecordingDurationMinutes = 16
        });

        _service = new DictationPersistenceService(
            _loggerMock.Object,
            _commandExecutorMock.Object,
            _defaultOptions);
    }

    #region SaveTranscriptionAsync - Success Cases

    [Fact]
    public async Task SaveTranscriptionAsync_WithoutLlmCorrection_SavesOnlyWhisperTranscription()
    {
        // Arrange
        var audioData = new byte[32000]; // 1 second of audio (16-bit mono @ 16kHz)
        var originalText = "Hello world";
        var expectedTranscription = new WhisperTranscription
        {
            Id = 123,
            TranscribedText = originalText,
            AudioDurationMs = 1000
        };

        _commandExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ICommand<WhisperTranscription>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTranscription);

        // Act
        var result = await _service.SaveTranscriptionAsync(
            audioData,
            originalText,
            correctionResult: null,  // No LLM correction
            CancellationToken.None);

        // Assert
        Assert.Equal(123, result);
        _commandExecutorMock.Verify(x => x.ExecuteAsync(
            It.Is<ICommand<WhisperTranscription>>(cmd =>
                cmd is Data.Commands.WhisperTranscriptionCommands.SaveWhisperTranscriptionCommand),
            It.IsAny<CancellationToken>()), Times.Once);
        _commandExecutorMock.Verify(x => x.ExecuteAsync(
            It.IsAny<ICommand<LlmCorrection>>(), It.IsAny<CancellationToken>()), Times.Never);
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

        _commandExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ICommand<WhisperTranscription>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTranscription);

        _commandExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ICommand<LlmCorrection>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCorrection);

        // Act
        var correctionResult = new LlmCorrectionResult(correctedText, null, llmDurationMs);
        var result = await _service.SaveTranscriptionAsync(
            audioData,
            originalText,
            correctionResult,
            CancellationToken.None);

        // Assert
        Assert.Equal(456, result);
        _commandExecutorMock.Verify(x => x.ExecuteAsync(
            It.Is<ICommand<WhisperTranscription>>(cmd =>
                cmd is Data.Commands.WhisperTranscriptionCommands.SaveWhisperTranscriptionCommand),
            It.IsAny<CancellationToken>()), Times.Once);
        _commandExecutorMock.Verify(x => x.ExecuteAsync(
            It.Is<ICommand<LlmCorrection>>(cmd =>
                cmd is Data.Commands.LlmCorrectionCommands.SaveLlmCorrectionCommand),
            It.IsAny<CancellationToken>()), Times.Once);
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

        _commandExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ICommand<WhisperTranscription>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTranscription);

        // Act
        var correctionResult = new LlmCorrectionResult(correctedText, null, 100);
        var result = await _service.SaveTranscriptionAsync(
            audioData,
            originalText,
            correctionResult,
            CancellationToken.None);

        // Assert
        Assert.Equal(111, result);
        _commandExecutorMock.Verify(x => x.ExecuteAsync(
            It.IsAny<ICommand<LlmCorrection>>(), It.IsAny<CancellationToken>()), Times.Never);
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

        _commandExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ICommand<WhisperTranscription>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhisperTranscription { Id = 1, TranscribedText = text, AudioDurationMs = expectedDurationMs });

        // Act
        await _service.SaveTranscriptionAsync(
            audioData,
            text,
            correctionResult: null,
            CancellationToken.None);

        // Assert
        _commandExecutorMock.Verify(
            x => x.ExecuteAsync(
                It.Is<ICommand<WhisperTranscription>>(cmd =>
                    cmd is Data.Commands.WhisperTranscriptionCommands.SaveWhisperTranscriptionCommand),
                It.IsAny<CancellationToken>()),
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

        _commandExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ICommand<WhisperTranscription>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _service.SaveTranscriptionAsync(
            audioData,
            text,
            correctionResult: null,
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

        _commandExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ICommand<WhisperTranscription>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhisperTranscription { Id = 999, TranscribedText = originalText, AudioDurationMs = 500 });

        _commandExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ICommand<LlmCorrection>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM save failed"));

        // Act
        var correctionResult = new LlmCorrectionResult(correctedText, null, 100);
        var result = await _service.SaveTranscriptionAsync(
            audioData,
            originalText,
            correctionResult,
            CancellationToken.None);

        // Assert
        // Service should return transcription ID even when LLM save fails (graceful degradation)
        Assert.Equal(999, result);
    }

    #endregion

    #region SaveTranscriptionAsync - Input Validation

    [Fact]
    public async Task SaveTranscriptionAsync_WithNullAudioData_ThrowsArgumentException()
    {
        // Arrange
        byte[]? audioData = null;
        var text = "test";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SaveTranscriptionAsync(audioData!, text, null, CancellationToken.None));

        Assert.Equal("audioData", exception.ParamName);
        Assert.Contains("cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task SaveTranscriptionAsync_WithEmptyAudioData_ThrowsArgumentException()
    {
        // Arrange
        var audioData = Array.Empty<byte>();
        var text = "test";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SaveTranscriptionAsync(audioData, text, null, CancellationToken.None));

        Assert.Equal("audioData", exception.ParamName);
        Assert.Contains("cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task SaveTranscriptionAsync_WithNullOriginalText_ThrowsArgumentException()
    {
        // Arrange
        var audioData = new byte[1000];
        string? originalText = null;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SaveTranscriptionAsync(audioData, originalText!, null, CancellationToken.None));

        Assert.Equal("originalText", exception.ParamName);
        Assert.Contains("cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task SaveTranscriptionAsync_WithEmptyOriginalText_ThrowsArgumentException()
    {
        // Arrange
        var audioData = new byte[1000];
        var originalText = "";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SaveTranscriptionAsync(audioData, originalText, null, CancellationToken.None));

        Assert.Equal("originalText", exception.ParamName);
        Assert.Contains("cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task SaveTranscriptionAsync_WithWhitespaceOriginalText_ThrowsArgumentException()
    {
        // Arrange
        var audioData = new byte[1000];
        var originalText = "   ";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SaveTranscriptionAsync(audioData, originalText, null, CancellationToken.None));

        Assert.Equal("originalText", exception.ParamName);
        Assert.Contains("cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task SaveTranscriptionAsync_WithOddLengthAudioData_LogsWarningButSucceeds()
    {
        // Arrange
        var audioData = new byte[15001]; // Odd length (not divisible by 2)
        var text = "test";
        var expectedDurationMs = 468; // 15001 / 2 (integer division) = 7500 samples, 7500 / 16000 * 1000 = 468.75ms -> 468ms

        _commandExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ICommand<WhisperTranscription>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhisperTranscription { Id = 1, TranscribedText = text, AudioDurationMs = expectedDurationMs });

        // Act
        var result = await _service.SaveTranscriptionAsync(audioData, text, null, CancellationToken.None);

        // Assert
        Assert.Equal(1, result);
        _commandExecutorMock.Verify(
            x => x.ExecuteAsync(
                It.Is<ICommand<WhisperTranscription>>(cmd =>
                    cmd is Data.Commands.WhisperTranscriptionCommands.SaveWhisperTranscriptionCommand),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not divisible")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
