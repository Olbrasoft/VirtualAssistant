using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Voice.Filters;

namespace Olbrasoft.VirtualAssistant.Voice.Tests.Filters;

/// <summary>
/// Verifies that <see cref="DatabaseCorrectionFilterStrategy.InvalidateCache"/>
/// forces the next <c>Apply</c> to reload corrections from the repository,
/// which is the contract the tray-menu "Obnovit cache" button depends on.
/// </summary>
public class DatabaseCorrectionFilterStrategyInvalidateCacheTests
{
    private static DatabaseCorrectionFilterStrategy CreateStrategy(Mock<ITranscriptionCorrectionRepository> repo)
    {
        return new DatabaseCorrectionFilterStrategy(
            Mock.Of<ILogger<DatabaseCorrectionFilterStrategy>>(),
            repo.Object);
    }

    private static TranscriptionCorrection BuildCorrection(int id, string incorrect, string correct)
    {
        return new TranscriptionCorrection
        {
            Id = id,
            IncorrectText = incorrect,
            CorrectText = correct,
            CaseSensitive = false,
            Priority = 90,
            IsActive = true
        };
    }

    [Fact]
    public void Apply_CachesAfterFirstCall_DoesNotRefetchOnSecondCall()
    {
        // Baseline: without invalidation, cache is sticky. This pins the behavior
        // so a later test showing the opposite is meaningful.
        var repo = new Mock<ITranscriptionCorrectionRepository>();
        repo.Setup(r => r.GetActiveCorrectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { BuildCorrection(1, "foo", "bar") });

        var strategy = CreateStrategy(repo);

        strategy.Apply("x");
        strategy.Apply("y");

        repo.Verify(r => r.GetActiveCorrectionsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void InvalidateCache_ForcesNextApplyToReloadFromRepository()
    {
        // Full contract: Apply once → cached; Invalidate → next Apply re-queries.
        var repo = new Mock<ITranscriptionCorrectionRepository>();
        repo.Setup(r => r.GetActiveCorrectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { BuildCorrection(1, "foo", "bar") });

        var strategy = CreateStrategy(repo);

        strategy.Apply("x");
        strategy.InvalidateCache();
        strategy.Apply("y");

        repo.Verify(r => r.GetActiveCorrectionsAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public void InvalidateCache_NextApplyUsesFreshlyInsertedCorrection()
    {
        // Simulates the real flow: user INSERTs a row, clicks the tray button,
        // the very next dictation already applies the new correction. The mock
        // returns an empty set first, then a non-empty set after invalidation.
        var firstCall = true;
        var repo = new Mock<ITranscriptionCorrectionRepository>();
        repo.Setup(r => r.GetActiveCorrectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (firstCall)
                {
                    firstCall = false;
                    return Array.Empty<TranscriptionCorrection>();
                }
                return new[] { BuildCorrection(1, "PlayWrite", "Playwright") };
            });

        var strategy = CreateStrategy(repo);

        var before = strategy.Apply("I use PlayWrite daily");
        strategy.InvalidateCache();
        var after = strategy.Apply("I use PlayWrite daily");

        Assert.Equal("I use PlayWrite daily", before);
        Assert.Equal("I use Playwright daily", after);
    }
}
