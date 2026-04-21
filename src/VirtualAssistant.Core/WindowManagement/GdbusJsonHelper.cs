using System.Diagnostics.CodeAnalysis;

namespace Olbrasoft.VirtualAssistant.Core.WindowManagement;

/// <summary>
/// Helpers for cleaning up JSON output extracted from <c>gdbus</c> /
/// GVariant string output before passing it to <c>System.Text.Json</c>.
/// </summary>
/// <remarks>
/// <para>
/// When gdbus prints a GVariant <c>(s,)</c> tuple, it wraps the string in
/// outer double-quotes and escapes every inner double-quote with a single
/// backslash. So a JSON payload of <c>[{"a":1}]</c> arrives on the wire as
/// <c>("[{\"a\":1}]",)</c> — i.e. every JSON quote is <c>\"</c> (two bytes:
/// <c>\</c> followed by <c>"</c>), not <c>\\"</c> (three bytes) as an earlier
/// version of this helper assumed.
/// </para>
/// <para>
/// A legitimate JSON escape inside a string value (say, a window title
/// containing a quote) therefore appears on the wire with one extra layer of
/// backslash-escaping: JSON <c>\"</c> becomes gdbus <c>\\\"</c>, and JSON
/// <c>\\</c> becomes gdbus <c>\\\\</c>. Stripping one layer of backslash
/// escaping is exactly what <see cref="UnescapeQuotes"/> does, preserving any
/// genuine JSON escape underneath. See #1047 for the failure mode that
/// motivated this.
/// </para>
/// </remarks>
public static class GdbusJsonHelper
{
    /// <summary>
    /// Strips one layer of gdbus GVariant backslash-escaping from
    /// <paramref name="json"/> so that <see cref="System.Text.Json.JsonSerializer"/>
    /// can parse it. Handles the two escape sequences gdbus emits for a
    /// GVariant <c>s</c> (string) wrapped in double-quotes:
    /// <list type="bullet">
    /// <item><c>\"</c> (backslash + quote) → <c>"</c></item>
    /// <item><c>\\</c> (two backslashes)   → <c>\</c></item>
    /// </list>
    /// Any other <c>\x</c> sequence is left alone so genuine JSON escapes
    /// inside string values (<c>\n</c>, <c>\t</c>, <c>\uXXXX</c>, …) survive.
    /// </summary>
    /// <param name="json">JSON snippet extracted from gdbus output, or
    /// <c>null</c>/empty if no payload was available.</param>
    /// <returns>The same JSON with one layer of gdbus escaping removed, or
    /// the original <paramref name="json"/> (including <c>null</c>) when the
    /// input is empty.</returns>
    [return: NotNullIfNotNull(nameof(json))]
    public static string? UnescapeQuotes(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return json;
        }

        // Single-pass scan: `\\` collapses to `\` and `\"` collapses to `"`.
        // Anything else — including genuine JSON escapes like `\n` or `\uXXXX`
        // and lone trailing backslashes — is emitted unchanged. A sentinel-
        // marker approach was considered first but would misbehave if the
        // marker ever appeared in the payload (e.g. inside a window title),
        // so we iterate characters directly.
        var builder = new System.Text.StringBuilder(json.Length);
        for (var i = 0; i < json.Length; i++)
        {
            var current = json[i];
            if (current == '\\' && i + 1 < json.Length)
            {
                var next = json[i + 1];
                if (next == '\\' || next == '"')
                {
                    builder.Append(next);
                    i++;
                    continue;
                }
            }
            builder.Append(current);
        }

        return builder.ToString();
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
