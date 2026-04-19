using Microsoft.Extensions.DependencyInjection;
using Moq;
using Olbrasoft.VirtualAssistant.Core.Models;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Core.Speech;
using Olbrasoft.VirtualAssistant.Service.Workers.Dictation;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Workers.Dictation;

/// <summary>
/// Pins the logic pulled out of DictationWorker.SaveTranscriptionToDatabaseAsync:
/// null LlmCorrectionResult when the LLM didn't actually correct, the Whisper
/// (13) fallback for a missing SttProviderId, and the racing-vs-non-racing
/// dispatch on RaceGroupId. These assertions used to live in the worker's
/// SkipOnCIFact integration tests; moving them here makes them fast unit
/// tests independent of the full worker plumbing.
/// </summary>
public class DictationTranscriptionPersisterTests
{
    private readonly Mock<IDictationPersistenceService> _persistenceMock = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();

    private DictationTranscriptionPersister CreateSut()
    {
        var scope = new Mock<IServiceScope>();
        var serviceProvider = new Mock<IServiceProvider>();
        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(scope.Object);
        scope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);
        serviceProvider.Setup(x => x.GetService(typeof(IDictationPersistenceService)))
            .Returns(_persistenceMock.Object);
        return new DictationTranscriptionPersister(_scopeFactoryMock.Object);
    }

    [Fact]
    public async Task SaveAsync_NoCorrection_CallsPlainSave_WithNullCorrectionResult()
    {
        // OriginalText == Text means the LLM pass was a no-op (or didn't run).
        // Persister must forward correctionResult=null rather than manufacturing
        // a zero-duration correction row.
        var sut = CreateSut();
        var audio = new byte[] { 1, 2, 3 };
        var result = new TranscriptionResult("hello", 0.9f)
        {
            OriginalText = "hello",
            SttProviderId = 14,
        };

        await sut.SaveAsync(audio, result, CancellationToken.None);

        _persistenceMock.Verify(x => x.SaveTranscriptionAsync(
            audio,
            "hello",
            null,
            14,
            It.IsAny<CancellationToken>()),
            Times.Once);
        _persistenceMock.Verify(x => x.SaveTranscriptionWithRacingAsync(
            It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<LlmCorrectionResult?>(),
            It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<Task<LlmCorrectionResult?>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveAsync_WithCorrection_BuildsCorrectionResultAndPreservesOriginalText()
    {
        var sut = CreateSut();
        var audio = new byte[] { 1 };
        var result = new TranscriptionResult("Hello World", 0.9f)
        {
            OriginalText = "hello world",
            PromptId = 42,
            ModelId = 3,
            LlmDurationMs = 150,
            InputTokens = 20,
            OutputTokens = 4,
            ReasoningTokens = 1,
            SttProviderId = 14,
        };

        await sut.SaveAsync(audio, result, CancellationToken.None);

        _persistenceMock.Verify(x => x.SaveTranscriptionAsync(
            audio,
            "hello world",
            It.Is<LlmCorrectionResult?>(c =>
                c != null &&
                c.CorrectedText == "Hello World" &&
                c.PromptId == 42 &&
                c.DurationMs == 150 &&
                c.ModelId == 3 &&
                c.InputTokens == 20 &&
                c.OutputTokens == 4 &&
                c.ReasoningTokens == 1),
            14,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveAsync_MissingSttProviderId_FallsBackToWhisperId13()
    {
        var sut = CreateSut();
        var audio = new byte[] { 9 };
        var result = new TranscriptionResult("x", 0.1f) { OriginalText = "x" };

        await sut.SaveAsync(audio, result, CancellationToken.None);

        _persistenceMock.Verify(x => x.SaveTranscriptionAsync(
            audio, "x", null, 13, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveAsync_ZeroLlmDuration_SkipsCorrectionResult()
    {
        // A missing/zero LlmDurationMs means LLM correction didn't actually
        // run even if OriginalText differs from Text (e.g. pre-seeded text).
        // Persister drops the correction to avoid writing a bogus duration.
        var sut = CreateSut();
        var result = new TranscriptionResult("corrected", 0.9f)
        {
            OriginalText = "original",
            ModelId = 3,
            LlmDurationMs = 0,
        };

        await sut.SaveAsync(new byte[] { 1 }, result, CancellationToken.None);

        _persistenceMock.Verify(x => x.SaveTranscriptionAsync(
            It.IsAny<byte[]>(), "original", null, It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveAsync_MissingModelId_SkipsCorrectionResult()
    {
        // LlmCorrectionResult requires ModelId (FK); without it persistence
        // can't record which model ran, so the correction is dropped entirely.
        var sut = CreateSut();
        var result = new TranscriptionResult("corrected", 0.9f)
        {
            OriginalText = "original",
            LlmDurationMs = 100,
            ModelId = null,
        };

        await sut.SaveAsync(new byte[] { 1 }, result, CancellationToken.None);

        _persistenceMock.Verify(x => x.SaveTranscriptionAsync(
            It.IsAny<byte[]>(), "original", null, It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SaveAsync_WithRaceGroupId_DispatchesToRacingSave()
    {
        var sut = CreateSut();
        var raceId = Guid.NewGuid();
        var loserTask = Task.FromResult<LlmCorrectionResult?>(null);
        var result = new TranscriptionResult("x", 0.9f)
        {
            OriginalText = "x",
            SttProviderId = 14,
            RaceGroupId = raceId,
            RacingLoserTask = loserTask,
        };

        await sut.SaveAsync(new byte[] { 1 }, result, CancellationToken.None);

        _persistenceMock.Verify(x => x.SaveTranscriptionWithRacingAsync(
            It.IsAny<byte[]>(),
            "x",
            null,
            14,
            raceId,
            loserTask,
            It.IsAny<CancellationToken>()),
            Times.Once);
        _persistenceMock.Verify(x => x.SaveTranscriptionAsync(
            It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<LlmCorrectionResult?>(),
            It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveAsync_UsesTextAsOriginalText_WhenOriginalTextNull()
    {
        // Backwards-compat: legacy transcribers that don't populate OriginalText
        // get their final Text stored as the original — the persistence table
        // still requires a non-null originalText column.
        var sut = CreateSut();
        var result = new TranscriptionResult("final", 0.9f) { OriginalText = null };

        await sut.SaveAsync(new byte[] { 1 }, result, CancellationToken.None);

        _persistenceMock.Verify(x => x.SaveTranscriptionAsync(
            It.IsAny<byte[]>(), "final", null, It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
