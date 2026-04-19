using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.VirtualAssistant.Voice.Filters;

namespace Olbrasoft.VirtualAssistant.Voice.Tests.Filters;

/// <summary>
/// Pins the "lomeno → /" rewrite. The strategy has to work for any word
/// pair (not one hardcoded phrase) so tests drive it with several shapes:
/// two-word path, chained segments, leading/trailing usage, case variants.
/// </summary>
public class PathSeparatorFilterStrategyTests
{
    private static PathSeparatorFilterStrategy CreateSut() =>
        new(Mock.Of<ILogger<PathSeparatorFilterStrategy>>());

    [Theory]
    [InlineData("Dokumenty lomeno přístupy", "Dokumenty/přístupy")]
    [InlineData("Dokumenty lomeno Olbrasoft lomeno projekt", "Dokumenty/Olbrasoft/projekt")]
    [InlineData("a lomeno b", "a/b")]
    [InlineData("konec lomeno", "konec/")]
    [InlineData("Lomeno začátek", "/začátek")]
    [InlineData("lomeno", "/")]
    public void Apply_ReplacesLomenoAndCollapsesSurroundingWhitespace(string input, string expected)
    {
        var sut = CreateSut();

        Assert.Equal(expected, sut.Apply(input));
    }

    [Theory]
    [InlineData("LOMENO")]
    [InlineData("Lomeno")]
    [InlineData("lomeno")]
    public void Apply_IsCaseInsensitive(string lomeno)
    {
        var sut = CreateSut();

        Assert.Equal("a/b", sut.Apply($"a {lomeno} b"));
    }

    [Fact]
    public void Apply_CollapsesMultipleSpacesAroundLomeno()
    {
        var sut = CreateSut();

        // Multiple spaces on both sides still collapse into a single slash
        // with no surrounding whitespace — WhitespaceFilterStrategy would
        // otherwise have to patch this up downstream.
        Assert.Equal("src/main", sut.Apply("src   lomeno   main"));
    }

    [Fact]
    public void Apply_WithoutLomeno_ReturnsInputUnchanged()
    {
        var sut = CreateSut();

        const string input = "Normální věta bez zvláštních slov.";
        Assert.Equal(input, sut.Apply(input));
    }

    [Fact]
    public void Apply_DoesNotTouchLomenoAsSubstringOfAnotherWord()
    {
        var sut = CreateSut();

        // Word boundary keeps embedded occurrences ("odlomeno", "zlomeno")
        // untouched — the user only means the standalone dictated word.
        Assert.Equal("Sklo zlomeno na půl.", sut.Apply("Sklo zlomeno na půl."));
    }

    [Fact]
    public void Apply_WithEmpty_ReturnsEmpty()
    {
        // ITextFilterStrategy.Apply contract is non-null string; null handling
        // is the lightweight filter's job. Empty string just round-trips.
        var sut = CreateSut();

        Assert.Equal(string.Empty, sut.Apply(string.Empty));
    }

    [Fact]
    public void IsEnabled_IsTrue()
    {
        var sut = CreateSut();
        Assert.True(sut.IsEnabled);
    }

    [Fact]
    public void Name_IsDescriptive()
    {
        var sut = CreateSut();
        Assert.Contains("lomeno", sut.Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ctor_NullLogger_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new PathSeparatorFilterStrategy(null!));
}
