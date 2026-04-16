using Olbrasoft.VirtualAssistant.Core.Utilities;

namespace Olbrasoft.VirtualAssistant.Core.Tests.Utilities;

public class RateLimitParserTests
{
    [Fact]
    public void ParseResetTimeFromError_WithMinutesAndSeconds_ReturnsCorrectTime()
    {
        var errorBody = "Rate limit exceeded. Please try again in 5m30.5s";
        var before = DateTime.UtcNow;

        var result = RateLimitParser.ParseResetTimeFromError(errorBody);

        Assert.NotNull(result);
        var expected = before.AddMinutes(5).AddSeconds(30.5);
        // Allow 2 seconds tolerance for test execution time
        Assert.True(result.Value >= expected.AddSeconds(-2) && result.Value <= expected.AddSeconds(2));
    }

    [Fact]
    public void ParseResetTimeFromError_WithOnlySeconds_ReturnsCorrectTime()
    {
        var errorBody = "Rate limit exceeded. Please try again in 45.5s";
        var before = DateTime.UtcNow;

        var result = RateLimitParser.ParseResetTimeFromError(errorBody);

        Assert.NotNull(result);
        var expected = before.AddSeconds(45.5);
        Assert.True(result.Value >= expected.AddSeconds(-2) && result.Value <= expected.AddSeconds(2));
    }

    [Fact]
    public void ParseResetTimeFromError_WithWholeSeconds_ReturnsCorrectTime()
    {
        var errorBody = "try again in 60s";
        var before = DateTime.UtcNow;

        var result = RateLimitParser.ParseResetTimeFromError(errorBody);

        Assert.NotNull(result);
        var expected = before.AddSeconds(60);
        Assert.True(result.Value >= expected.AddSeconds(-2) && result.Value <= expected.AddSeconds(2));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Rate limit exceeded")]
    [InlineData("Please wait")]
    [InlineData("Error occurred")]
    public void ParseResetTimeFromError_WithoutTimePattern_ReturnsNull(string errorBody)
    {
        var result = RateLimitParser.ParseResetTimeFromError(errorBody);

        Assert.Null(result);
    }

    [Fact]
    public void ParseResetTimeFromError_WithZeroMinutes_ReturnsCorrectTime()
    {
        var errorBody = "try again in 0m15s";
        var before = DateTime.UtcNow;

        var result = RateLimitParser.ParseResetTimeFromError(errorBody);

        Assert.NotNull(result);
        var expected = before.AddSeconds(15);
        Assert.True(result.Value >= expected.AddSeconds(-2) && result.Value <= expected.AddSeconds(2));
    }

    [Fact]
    public void ParseResetTimeFromError_WithLargeValues_ReturnsCorrectTime()
    {
        var errorBody = "Rate limit. try again in 10m120.5s";
        var before = DateTime.UtcNow;

        var result = RateLimitParser.ParseResetTimeFromError(errorBody);

        Assert.NotNull(result);
        var expected = before.AddMinutes(10).AddSeconds(120.5);
        Assert.True(result.Value >= expected.AddSeconds(-2) && result.Value <= expected.AddSeconds(2));
    }

    [Fact]
    public void ParseResetTimeFromError_WithSubSecondPrecision_ReturnsCorrectTime()
    {
        var errorBody = "try again in 0m0.25s";
        var before = DateTime.UtcNow;

        var result = RateLimitParser.ParseResetTimeFromError(errorBody);

        Assert.NotNull(result);
        var actualDelay = result.Value - before;
        var expectedDelay = TimeSpan.FromSeconds(0.25);
        var tolerance = TimeSpan.FromMilliseconds(100);
        Assert.True(actualDelay >= expectedDelay - tolerance && actualDelay <= expectedDelay + tolerance);
    }

    [Fact]
    public void ParseResetTimeFromError_WithNullInput_ReturnsNullWithoutThrowing()
    {
        var result = RateLimitParser.ParseResetTimeFromError(null!);

        Assert.Null(result);
    }
}
