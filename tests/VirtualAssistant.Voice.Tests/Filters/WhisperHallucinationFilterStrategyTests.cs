using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Voice.Filters;

namespace Olbrasoft.VirtualAssistant.Voice.Tests.Filters;

public class WhisperHallucinationFilterStrategyTests : IDisposable
{
    private readonly string _tempConfigPath;
    private readonly Mock<ILogger<WhisperHallucinationFilterStrategy>> _loggerMock;

    public WhisperHallucinationFilterStrategyTests()
    {
        _tempConfigPath = Path.Combine(
            Path.GetTempPath(),
            $"text-filters-test-{Guid.NewGuid():N}.json");
        _loggerMock = new Mock<ILogger<WhisperHallucinationFilterStrategy>>();
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

    private WhisperHallucinationFilterStrategy CreateStrategy()
        => new(_loggerMock.Object, _tempConfigPath);

    [Fact]
    public void Apply_WithNullConfigPath_ReturnsTextUnchanged()
    {
        var strategy = new WhisperHallucinationFilterStrategy(_loggerMock.Object, configPath: null);

        var result = strategy.Apply("Some text");

        Assert.Equal("Some text", result);
        Assert.False(strategy.IsEnabled);
    }

    [Fact]
    public void Apply_WhenWholeTextMatchesPattern_ReturnsEmpty()
    {
        WriteConfig(new TextFiltersConfig
        {
            RemoveWholeText = new() { "Konec.", "Konec" }
        });
        var strategy = CreateStrategy();

        var result = strategy.Apply("Konec.");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Apply_WhenWholeTextMatchesPatternCaseInsensitive_ReturnsEmpty()
    {
        WriteConfig(new TextFiltersConfig
        {
            RemoveWholeText = new() { "Konec." }
        });
        var strategy = CreateStrategy();

        var result = strategy.Apply("KONEC.");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Apply_WhenWholeTextMatchesAfterTrim_ReturnsEmpty()
    {
        WriteConfig(new TextFiltersConfig
        {
            RemoveWholeText = new() { "Konec." }
        });
        var strategy = CreateStrategy();

        var result = strategy.Apply("   Konec.   ");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Apply_WhenPatternIsSubstringOnly_PreservesText()
    {
        // Whole-text match must NOT damage longer legitimate sentences containing the pattern.
        WriteConfig(new TextFiltersConfig
        {
            RemoveWholeText = new() { "Konec." }
        });
        var strategy = CreateStrategy();

        var legitimate = "Konec konců, podle mě je to dobře.";
        var result = strategy.Apply(legitimate);

        Assert.Equal(legitimate, result);
    }

    [Fact]
    public void Apply_WhenSuffixRegexMatches_RemovesSuffixOnly()
    {
        WriteConfig(new TextFiltersConfig
        {
            RemoveSuffixRegex = new() { "\\s*Titulky vytvořil[^.]*\\.?\\s*" }
        });
        var strategy = CreateStrategy();

        var result = strategy.Apply("Otevři prosím tě tohle. Titulky vytvořil JohnyX.");

        Assert.Equal("Otevři prosím tě tohle.", result);
    }

    [Fact]
    public void Apply_WhenSuffixRegexMatchesNamedAuthor_RemovesSuffix()
    {
        WriteConfig(new TextFiltersConfig
        {
            RemoveSuffixRegex = new() { "\\s*Titulky vytvořil[^.]*\\.?\\s*" }
        });
        var strategy = CreateStrategy();

        var result = strategy.Apply("Tady je nějaký dlouhý text. Titulky vytvořil Jirka Kováč");

        Assert.Equal("Tady je nějaký dlouhý text.", result);
    }

    [Fact]
    public void Apply_WhenSuffixRegexDoesNotMatchAtEnd_LeavesTextUnchanged()
    {
        // The regex is anchored to end-of-text, so a match in the middle must NOT be removed.
        WriteConfig(new TextFiltersConfig
        {
            RemoveSuffixRegex = new() { "\\s*Titulky vytvořil[^.]*\\.?\\s*" }
        });
        var strategy = CreateStrategy();

        var input = "Titulky vytvořil JohnyX. A pak pokračuje normální text.";
        var result = strategy.Apply(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void Apply_WithNormalText_ReturnsUnchanged()
    {
        WriteConfig(new TextFiltersConfig
        {
            RemoveWholeText = new() { "Konec." },
            RemoveSuffixRegex = new() { "\\s*Titulky vytvořil[^.]*\\.?\\s*" }
        });
        var strategy = CreateStrategy();

        var input = "Najdi mi něco na seznam.cz, co bude užitečné.";
        var result = strategy.Apply(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void Apply_WithMultipleSuffixRegexes_AppliesAllInOrder()
    {
        WriteConfig(new TextFiltersConfig
        {
            RemoveSuffixRegex = new()
            {
                "\\s*Titulky vytvořil[^.]*\\.?\\s*",
                "\\s*Konec\\.\\s*"
            }
        });
        var strategy = CreateStrategy();

        var result = strategy.Apply("Hlavní text. Titulky vytvořil JohnyX. Konec.");

        // Both suffixes are matched and stripped (Konec. first, then Titulky vytvořil...).
        Assert.Equal("Hlavní text.", result);
    }

    [Fact]
    public void Apply_WithEmptyString_ReturnsEmpty()
    {
        WriteConfig(new TextFiltersConfig
        {
            RemoveWholeText = new() { "Konec." }
        });
        var strategy = CreateStrategy();

        var result = strategy.Apply("");

        Assert.Equal("", result);
    }

    [Fact]
    public void Apply_WithWhitespaceOnly_ReturnsUnchanged()
    {
        WriteConfig(new TextFiltersConfig
        {
            RemoveWholeText = new() { "Konec." }
        });
        var strategy = CreateStrategy();

        var input = "   ";
        var result = strategy.Apply(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void IsEnabled_WithNoPatterns_ReturnsFalse()
    {
        WriteConfig(new TextFiltersConfig
        {
            RemoveWholeText = new(),
            RemoveSuffixRegex = new()
        });
        var strategy = CreateStrategy();

        Assert.False(strategy.IsEnabled);
    }

    [Fact]
    public void IsEnabled_WithWholeTextOnly_ReturnsTrue()
    {
        WriteConfig(new TextFiltersConfig
        {
            RemoveWholeText = new() { "Konec." }
        });
        var strategy = CreateStrategy();

        Assert.True(strategy.IsEnabled);
    }

    [Fact]
    public void IsEnabled_WithSuffixRegexOnly_ReturnsTrue()
    {
        WriteConfig(new TextFiltersConfig
        {
            RemoveSuffixRegex = new() { "\\s*Titulky[^$]*" }
        });
        var strategy = CreateStrategy();

        Assert.True(strategy.IsEnabled);
    }

    [Fact]
    public void Apply_WithInvalidRegex_SkipsInvalidAndContinues()
    {
        WriteConfig(new TextFiltersConfig
        {
            RemoveSuffixRegex = new()
            {
                "[invalid(regex",  // bad pattern
                "\\s*Konec\\.\\s*"
            }
        });
        var strategy = CreateStrategy();

        // Bad regex is logged and skipped, valid one still works.
        var result = strategy.Apply("Hello there. Konec.");

        Assert.Equal("Hello there.", result);
    }

    [Fact]
    public void Apply_WhenConfigFileChanges_HotReloadsPatterns()
    {
        WriteConfig(new TextFiltersConfig
        {
            RemoveWholeText = new() { "Konec." }
        });
        var strategy = CreateStrategy();

        // First call: synthetic pattern is NOT in config yet → text is preserved
        var first = strategy.Apply("__SYNTHETIC_HALLUCINATION__");
        Assert.Equal("__SYNTHETIC_HALLUCINATION__", first);

        // Update the config file with the new pattern. Force a different last-write timestamp
        // because some filesystems have second-level granularity.
        File.SetLastWriteTimeUtc(_tempConfigPath, DateTime.UtcNow.AddSeconds(-10));
        WriteConfig(new TextFiltersConfig
        {
            RemoveWholeText = new() { "Konec.", "__SYNTHETIC_HALLUCINATION__" }
        });
        File.SetLastWriteTimeUtc(_tempConfigPath, DateTime.UtcNow);

        // Second call: pattern is now configured → text is wiped
        var second = strategy.Apply("__SYNTHETIC_HALLUCINATION__");
        Assert.Equal(string.Empty, second);
    }
}
