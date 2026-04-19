using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Data.Entities;
using Olbrasoft.VirtualAssistant.Voice.Filters;

namespace Olbrasoft.VirtualAssistant.Voice.Tests.Filters;

public class LightweightTextFilterTests : IDisposable
{
    private readonly string _tempConfigPath;

    public LightweightTextFilterTests()
    {
        _tempConfigPath = Path.Combine(
            Path.GetTempPath(),
            $"text-filters-lightweight-test-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempConfigPath))
        {
            File.Delete(_tempConfigPath);
        }
    }

    private void WriteConfig(TextFiltersConfig config)
    {
        var json = JsonSerializer.Serialize(config);
        File.WriteAllText(_tempConfigPath, json);
    }

    private LightweightTextFilter CreateFilter()
    {
        var hallucination = new WhisperHallucinationFilterStrategy(
            Mock.Of<ILogger<WhisperHallucinationFilterStrategy>>(),
            _tempConfigPath);
        // Pass null repository so DatabaseCorrectionFilterStrategy is disabled in tests —
        // we cover its behavior in DatabaseCorrectionFilterStrategyTests.
        var dbCorrections = new DatabaseCorrectionFilterStrategy(
            Mock.Of<ILogger<DatabaseCorrectionFilterStrategy>>(),
            repository: null);
        var pathSeparator = new PathSeparatorFilterStrategy(
            Mock.Of<ILogger<PathSeparatorFilterStrategy>>());
        var whitespace = new WhitespaceFilterStrategy(
            Mock.Of<ILogger<WhitespaceFilterStrategy>>());
        var logger = Mock.Of<ILogger<LightweightTextFilter>>();
        return new LightweightTextFilter(hallucination, dbCorrections, pathSeparator, whitespace, logger);
    }

    [Fact]
    public void Apply_WithNullText_ReturnsEmpty()
    {
        WriteConfig(new TextFiltersConfig());
        var filter = CreateFilter();

        Assert.Equal(string.Empty, filter.Apply(null));
    }

    [Fact]
    public void Apply_WithWhitespaceText_ReturnsEmpty()
    {
        WriteConfig(new TextFiltersConfig());
        var filter = CreateFilter();

        Assert.Equal(string.Empty, filter.Apply("   "));
    }

    [Fact]
    public void Apply_WhenHallucinationStrategyWipesText_ReturnsEmpty()
    {
        WriteConfig(new TextFiltersConfig
        {
            RemoveWholeText = new() { "Konec." }
        });
        var filter = CreateFilter();

        Assert.Equal(string.Empty, filter.Apply("Konec."));
    }

    [Fact]
    public void Apply_WithNormalText_AppliesWhitespaceNormalization()
    {
        WriteConfig(new TextFiltersConfig());
        var filter = CreateFilter();

        var result = filter.Apply("  Hello   world.  ");

        Assert.Equal("Hello world.", result);
    }

    [Fact]
    public void Apply_WithSuffixHallucinationAndExtraSpaces_RemovesAndNormalizes()
    {
        WriteConfig(new TextFiltersConfig
        {
            RemoveSuffixRegex = new() { "\\s*Titulky vytvořil[^.]*\\.?\\s*" }
        });
        var filter = CreateFilter();

        var result = filter.Apply("  Otevři tohle.   Titulky vytvořil JohnyX.  ");

        Assert.Equal("Otevři tohle.", result);
    }

    [Fact]
    public void Apply_NeverInvokesLlm()
    {
        // The lightweight filter is constructed with concrete strategy types via the
        // constructor — there is no ILlmProviderFactory or IRacingLlmProvider dependency,
        // so no LLM call is reachable from this code path. The DatabaseCorrectionFilterStrategy
        // IS injected (sub-millisecond cache lookup), so this test deliberately uses a
        // null repository to keep the DB strategy disabled and verify only that LLM is not
        // touched.
        WriteConfig(new TextFiltersConfig());
        var filter = CreateFilter();

        // No throw, no LLM call.
        var result = filter.Apply("plain text");
        Assert.Equal("plain text", result);
    }

    [Fact]
    public void Apply_AppliesDatabaseCorrectionsBeforeWhitespace()
    {
        // This test locks in the ORDER: DB corrections must run BEFORE whitespace
        // normalization. We pick a correction whose CORRECT_TEXT contains a double
        // space ("Claude  Code"). If the order is correct (DB → whitespace), the
        // double space introduced by the correction is collapsed by whitespace and
        // the final result has a single space ("Claude Code"). If whitespace ran
        // FIRST and DB ran second, the double space would survive in the output
        // ("Claude  Code"), and the assertion would fail. The input has no double
        // spaces, so whitespace alone has nothing to do; only the correction can
        // introduce them.
        WriteConfig(new TextFiltersConfig());

        var hallucination = new WhisperHallucinationFilterStrategy(
            Mock.Of<ILogger<WhisperHallucinationFilterStrategy>>(),
            _tempConfigPath);
        var fakeRepo = new Mock<ITranscriptionCorrectionRepository>();
        fakeRepo.Setup(r => r.GetActiveCorrectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TranscriptionCorrection>
            {
                new()
                {
                    Id = 1,
                    IncorrectText = "cloud kód",
                    CorrectText = "Claude  Code",  // double space — collapsed only if whitespace runs after
                    Priority = 90,
                    IsActive = true
                }
            });
        var dbCorrections = new DatabaseCorrectionFilterStrategy(
            Mock.Of<ILogger<DatabaseCorrectionFilterStrategy>>(),
            fakeRepo.Object);
        var pathSeparator = new PathSeparatorFilterStrategy(
            Mock.Of<ILogger<PathSeparatorFilterStrategy>>());
        var whitespace = new WhitespaceFilterStrategy(
            Mock.Of<ILogger<WhitespaceFilterStrategy>>());
        var filter = new LightweightTextFilter(
            hallucination,
            dbCorrections,
            pathSeparator,
            whitespace,
            Mock.Of<ILogger<LightweightTextFilter>>());

        // Input has NO double spaces, so whitespace alone would not change anything
        // related to the term. Only the DB correction introduces the double space,
        // and only the post-correction whitespace pass collapses it.
        var result = filter.Apply("Otevři cloud kód a najdi mi tohle.");

        // Single space proves: DB ran first (introduced "Claude  Code"), whitespace
        // ran second (collapsed it to "Claude Code").
        Assert.Equal("Otevři Claude Code a najdi mi tohle.", result);
    }

    [Fact]
    public void IsEnabled_WhenWhitespaceStrategyAlwaysOn_ReturnsTrue()
    {
        WriteConfig(new TextFiltersConfig());
        var filter = CreateFilter();

        // WhitespaceFilterStrategy.IsEnabled is always true.
        Assert.True(filter.IsEnabled);
    }

    [Fact]
    public void Apply_LomenoBetweenWords_ProducesJoinedPath()
    {
        // End-to-end pin for the Quick Dictation path scenario that triggered
        // this feature: user dictates "Dokumenty lomeno přístupy" and expects
        // "Dokumenty/přístupy" — no spaces around the slash — even without
        // LLM correction in the loop.
        WriteConfig(new TextFiltersConfig());
        var filter = CreateFilter();

        var result = filter.Apply("Dokumenty lomeno přístupy");

        Assert.Equal("Dokumenty/přístupy", result);
    }
}
