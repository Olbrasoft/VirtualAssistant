using System.Text.Json;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;

namespace Olbrasoft.VirtualAssistant.Core.Tests.WindowManagement;

public class GdbusJsonHelperTests
{
    [Fact]
    public void UnescapeQuotes_WithGdbusEscapedJson_ProducesValidJson()
    {
        // gdbus wraps strings in "..." and escapes inner " as \"
        // After extracting from [ to ], structural quotes appear as \"
        // Input: [{\"wm_class\":\"kitty\",\"focus\":true}]
        var input = "[{\\\"wm_class\\\":\\\"kitty\\\",\\\"focus\\\":true}]";

        var result = GdbusJsonHelper.UnescapeQuotes(input);

        Assert.Equal("[{\"wm_class\":\"kitty\",\"focus\":true}]", result);

        // Verify it's valid JSON
        var parsed = JsonSerializer.Deserialize<JsonElement>(result);
        var first = parsed.EnumerateArray().First();
        Assert.Equal("kitty", first.GetProperty("wm_class").GetString());
        Assert.True(first.GetProperty("focus").GetBoolean());
    }

    [Fact]
    public void UnescapeQuotes_WithEmbeddedQuotesInTitle_ProducesValidJson()
    {
        // Window title: Hello "World"
        // In JSON: "title":"Hello \"World\""
        // After gdbus escaping: \"title\":\"Hello \\\"World\\\"\"
        // Extracted from [ to ]: [{\"title\":\"Hello \\\\\\\"World\\\\\\\"\"}]
        // which as actual chars is: [{\"title\":\"Hello \\\"World\\\"\"}]
        var input = "[{\\\"title\\\":\\\"Hello \\\\\\\"World\\\\\\\"\\\"}]";

        var result = GdbusJsonHelper.UnescapeQuotes(input);

        var parsed = JsonSerializer.Deserialize<JsonElement>(result);
        var first = parsed.EnumerateArray().First();
        Assert.Equal("Hello \"World\"", first.GetProperty("title").GetString());
    }

    [Fact]
    public void UnescapeQuotes_WithAlreadyValidJson_ReturnsUnchanged()
    {
        // Input that is already valid JSON (no backslash escaping)
        var input = "[{\"wm_class\":\"firefox\",\"focus\":true}]";

        var result = GdbusJsonHelper.UnescapeQuotes(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void UnescapeQuotes_WithEmptyString_ReturnsEmpty()
    {
        var result = GdbusJsonHelper.UnescapeQuotes(string.Empty);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void UnescapeQuotes_WithNull_ReturnsNull()
    {
        var result = GdbusJsonHelper.UnescapeQuotes(null!);

        Assert.Null(result);
    }

    [Fact]
    public void UnescapeQuotes_WithMultipleEscapedProperties_ProducesValidJson()
    {
        // Multiple properties with gdbus escaping
        // Actual chars: [{"a":"x","b":"y"}] after unescape
        var input = "[{\\\"a\\\":\\\"x\\\",\\\"b\\\":\\\"y\\\"}]";

        var result = GdbusJsonHelper.UnescapeQuotes(input);

        var parsed = JsonSerializer.Deserialize<JsonElement>(result);
        var first = parsed.EnumerateArray().First();
        Assert.Equal("x", first.GetProperty("a").GetString());
        Assert.Equal("y", first.GetProperty("b").GetString());
    }

    [Fact]
    public void UnescapeQuotes_WithRealGdbusOutput_ParsesCorrectly()
    {
        // Real-world gdbus output snippet (extracted from [ to ])
        // with terminator window having focus
        var input = "[{\\\"wm_class\\\":\\\"terminator\\\",\\\"title\\\":\\\"Claude Code\\\",\\\"focus\\\":true}]";

        var result = GdbusJsonHelper.UnescapeQuotes(input);

        var parsed = JsonSerializer.Deserialize<JsonElement>(result);
        var first = parsed.EnumerateArray().First();
        Assert.Equal("terminator", first.GetProperty("wm_class").GetString());
        Assert.Equal("Claude Code", first.GetProperty("title").GetString());
        Assert.True(first.GetProperty("focus").GetBoolean());
    }

    [Fact]
    public void UnescapeQuotes_TrailingBackslash_PreservedAsIs()
    {
        // Edge case: trailing backslash without a following character
        var input = "test\\";

        var result = GdbusJsonHelper.UnescapeQuotes(input);

        Assert.Equal("test\\", result);
    }
}
