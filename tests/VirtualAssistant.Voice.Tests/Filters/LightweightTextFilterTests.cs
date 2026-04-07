using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
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
        var whitespace = new WhitespaceFilterStrategy(
            Mock.Of<ILogger<WhitespaceFilterStrategy>>());
        var logger = Mock.Of<ILogger<LightweightTextFilter>>();
        return new LightweightTextFilter(hallucination, whitespace, logger);
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
    public void Apply_NeverInvokesDatabaseOrLlm()
    {
        // The lightweight filter takes only the two concrete strategies via constructor;
        // there is no IEnumerable<ITextFilterStrategy> dependency that could leak DB or
        // LLM strategies in. This test exists as documentation of the contract.
        WriteConfig(new TextFiltersConfig());
        var filter = CreateFilter();

        // No throw, no DB call, no LLM call.
        var result = filter.Apply("plain text");
        Assert.Equal("plain text", result);
    }

    [Fact]
    public void IsEnabled_WhenWhitespaceStrategyAlwaysOn_ReturnsTrue()
    {
        WriteConfig(new TextFiltersConfig());
        var filter = CreateFilter();

        // WhitespaceFilterStrategy.IsEnabled is always true.
        Assert.True(filter.IsEnabled);
    }
}
