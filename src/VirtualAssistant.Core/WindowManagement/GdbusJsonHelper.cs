namespace Olbrasoft.VirtualAssistant.Core.WindowManagement;

/// <summary>
/// Helpers for cleaning up JSON output extracted from <c>gdbus</c> /
/// GVariant string output before passing it to <c>System.Text.Json</c>.
/// </summary>
/// <remarks>
/// gdbus prints GVariant <c>s</c> (string) values with the inner quotes
/// already escaped — so a JSON value of <c>"a\"b"</c> arrives wrapped in
/// the GVariant outer quoting as <c>"\"a\\\"b\""</c>. By the time we
/// substring out the JSON array, embedded quotes look like <c>\\"</c>
/// instead of the standard JSON <c>\"</c>, which makes
/// <see cref="System.Text.Json.JsonSerializer"/> fail to deserialize.
/// All call sites that read JSON from <c>gdbus</c> output need to apply
/// the same one-liner unescape, so it lives here as a single source of
/// truth.
/// </remarks>
public static class GdbusJsonHelper
{
    /// <summary>
    /// Collapses double-escaped quotes (<c>\\"</c>) emitted by gdbus's
    /// GVariant string formatter back to standard JSON-escaped quotes
    /// (<c>\"</c>) so that <see cref="System.Text.Json.JsonSerializer"/>
    /// can parse the value.
    /// </summary>
    /// <param name="json">JSON snippet extracted from gdbus output.</param>
    /// <returns>The same JSON with quote escaping normalized.</returns>
    public static string UnescapeQuotes(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return json;
        }

        return json.Replace("\\\\\"", "\\\"");
    }

    /// <summary>
    /// Extracts the substring from the first <c>[</c> to the last <c>]</c>
    /// found in raw <c>gdbus</c> output wrapped in the GVariant tuple prefix,
    /// e.g. <c>('[{…}]',)</c>. Returns <c>null</c> when no opening bracket is
    /// found or when no later closing bracket can be located.
    /// </summary>
    /// <param name="rawOutput">Unprocessed stdout from a gdbus call.</param>
    /// <returns>The JSON array substring, or <c>null</c> if not present.</returns>
    public static string? TryExtractJsonArray(string? rawOutput)
    {
        if (string.IsNullOrEmpty(rawOutput))
        {
            return null;
        }

        var jsonStart = rawOutput.IndexOf('[');
        var jsonEnd = rawOutput.LastIndexOf(']');
        if (jsonStart < 0 || jsonEnd <= jsonStart)
        {
            return null;
        }

        return rawOutput.Substring(jsonStart, jsonEnd - jsonStart + 1);
    }
}
