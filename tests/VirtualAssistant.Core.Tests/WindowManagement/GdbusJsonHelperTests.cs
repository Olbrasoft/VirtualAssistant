using System.Text.Json;
using Olbrasoft.VirtualAssistant.Core.WindowManagement;

namespace Olbrasoft.VirtualAssistant.Core.Tests.WindowManagement;

public class GdbusJsonHelperTests
{
    [Fact]
    public void UnescapeQuotes_WithDoubleEscapedQuote_CollapsesToSingleEscape()
    {
        // gdbus emits embedded quotes as \\" — JSON parser needs \"
        var input = "[{\"title\":\"Hello \\\\\"World\\\\\"\"}]";

        var result = GdbusJsonHelper.UnescapeQuotes(input);

        Assert.Equal("[{\"title\":\"Hello \\\"World\\\"\"}]", result);
    }

    [Fact]
    public void UnescapeQuotes_WithNormalJson_ReturnsUnchanged()
    {
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
    public void UnescapeQuotes_WithMultipleDoubleEscapedQuotes_CollapsesAll()
    {
        // Two embedded quotes in one string
        var input = "{\"a\":\"x\\\\\"y\\\\\"z\"}";

        var result = GdbusJsonHelper.UnescapeQuotes(input);

        Assert.Equal("{\"a\":\"x\\\"y\\\"z\"}", result);
    }

    [Fact]
    public void UnescapeQuotes_OutputIsValidJson_WhenInputContainsEmbeddedQuoteInTitle()
    {
        // Realistic case: window title 'a "quoted" title' arrives from gdbus
        // serialized as {"title":"a \\"quoted\\" title"} which is invalid JSON.
        // After unescaping it becomes {"title":"a \"quoted\" title"} which is
        // valid JSON and System.Text.Json can deserialize it.
        var malformed = "[{\"title\":\"a \\\\\"quoted\\\\\" title\"}]";

        var fixedJson = GdbusJsonHelper.UnescapeQuotes(malformed);

        var parsed = JsonSerializer.Deserialize<JsonElement>(fixedJson);
        var first = parsed.EnumerateArray().First();
        Assert.Equal("a \"quoted\" title", first.GetProperty("title").GetString());
    }

    [Fact]
    public void UnescapeQuotes_RealGdbusWireFormat_ProducesValidJson()
    {
        // Pin the real gdbus output format against a byte sequence captured with
        // `od -c` from a live `gdbus call ... Windows.List` on this host.
        // Positions 4-5 of the raw tuple are `\` then `"` — single-escape — so
        // after TryExtractJsonArray strips the `("..",)` wrapper the substring
        // carries one backslash per JSON quote. Previous code hunted for a
        // non-existent double-escape (`\\"`) and left every `\"` in place, which
        // made System.Text.Json throw on byte position 2 of the first property
        // name (#1047).
        var wireSubstring =
            "[{\\\"in_current_workspace\\\":false,\\\"workspace\\\":1," +
            "\\\"wm_class\\\":\\\"doublecmd\\\",\\\"title\\\":\\\"Double Commander\\\"," +
            "\\\"pid\\\":6143,\\\"focus\\\":false}," +
            "{\\\"in_current_workspace\\\":true,\\\"workspace\\\":7," +
            "\\\"wm_class\\\":\\\"terminator\\\",\\\"title\\\":\\\"/bin/bash\\\"," +
            "\\\"pid\\\":322375,\\\"focus\\\":true}]";

        var jsonArray = GdbusJsonHelper.UnescapeQuotes(wireSubstring);

        var parsed = JsonSerializer.Deserialize<JsonElement>(jsonArray);
        var focused = parsed.EnumerateArray()
            .First(w => w.GetProperty("focus").GetBoolean());
        Assert.Equal("terminator", focused.GetProperty("wm_class").GetString());
        Assert.Equal("/bin/bash", focused.GetProperty("title").GetString());
        Assert.Equal(322375, focused.GetProperty("pid").GetInt32());
    }

    [Fact]
    public void UnescapeQuotes_SingleBackslashFollowedByNonQuote_IsPreserved()
    {
        // Inside a JSON string value, escape sequences other than \" and \\
        // (e.g. \n, \t, \uXXXX) must survive the pass so the downstream JSON
        // parser sees a valid string literal.
        var input = "{\"msg\":\"line1\\nline2\\u00e9\"}";

        var result = GdbusJsonHelper.UnescapeQuotes(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void TryExtractJsonArray_WithGdbusTuplePrefix_ReturnsArraySubstring()
    {
        // Happy path: gdbus emits a single-element tuple with a string value,
        // e.g. ('[{"focus":true}]',) — we want the inner JSON array.
        var raw = "('[{\"focus\":true,\"wm_class\":\"kitty\"}]',)";

        var result = GdbusJsonHelper.TryExtractJsonArray(raw);

        Assert.Equal("[{\"focus\":true,\"wm_class\":\"kitty\"}]", result);
    }

    [Fact]
    public void TryExtractJsonArray_WithNoBrackets_ReturnsNull()
    {
        // Edge case: gdbus returned an error/empty payload without any brackets.
        var raw = "no data";

        var result = GdbusJsonHelper.TryExtractJsonArray(raw);

        Assert.Null(result);
    }

    [Fact]
    public void TryExtractJsonArray_WithNullInput_ReturnsNull()
    {
        var result = GdbusJsonHelper.TryExtractJsonArray(null);

        Assert.Null(result);
    }

    [Fact]
    public void TryExtractJsonArray_WithOnlyOpeningBracket_ReturnsNull()
    {
        // Malformed: opening bracket but no closing one — do not return a
        // half-formed substring.
        var raw = "('[broken',)";

        var result = GdbusJsonHelper.TryExtractJsonArray(raw);

        Assert.Null(result);
    }
}
