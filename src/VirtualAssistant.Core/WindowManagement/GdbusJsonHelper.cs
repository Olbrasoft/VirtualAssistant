using System.Text;

namespace Olbrasoft.VirtualAssistant.Core.WindowManagement;

/// <summary>
/// Helpers for cleaning up JSON output extracted from <c>gdbus</c> /
/// GVariant string output before passing it to <c>System.Text.Json</c>.
/// </summary>
/// <remarks>
/// gdbus prints GVariant <c>s</c> (string) values using C-style escaping
/// inside double quotes: literal <c>"</c> becomes <c>\"</c> and literal
/// <c>\</c> becomes <c>\\</c>. When we extract the JSON array from between
/// <c>[</c> and <c>]</c>, the structural JSON quotes appear as <c>\"</c>
/// and any backslash-escaped quotes inside JSON string values appear as
/// <c>\\\"</c>. This helper undoes the GVariant escaping so that
/// <see cref="System.Text.Json.JsonSerializer"/> can parse the result.
/// All call sites that read JSON from <c>gdbus</c> output need to apply
/// the same unescape, so it lives here as a single source of truth.
/// </remarks>
public static class GdbusJsonHelper
{
    /// <summary>
    /// Undoes GVariant C-style string escaping on a JSON snippet extracted
    /// from gdbus output. Processes escape sequences character by character:
    /// <c>\"</c> → <c>"</c> and <c>\\</c> → <c>\</c>.
    /// This correctly converts structural JSON quotes (<c>\"</c> → <c>"</c>)
    /// and preserves embedded quotes in JSON string values
    /// (<c>\\\"</c> → <c>\"</c>, which is valid JSON escaping).
    /// </summary>
    /// <param name="json">JSON snippet extracted from gdbus output.</param>
    /// <returns>The same JSON with GVariant escaping undone.</returns>
    public static string UnescapeQuotes(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return json;
        }

        var sb = new StringBuilder(json.Length);

        for (var i = 0; i < json.Length; i++)
        {
            if (json[i] == '\\' && i + 1 < json.Length)
            {
                switch (json[i + 1])
                {
                    case '"':
                        sb.Append('"');
                        i++;
                        break;
                    case '\\':
                        sb.Append('\\');
                        i++;
                        break;
                    default:
                        sb.Append(json[i]);
                        break;
                }
            }
            else
            {
                sb.Append(json[i]);
            }
        }

        return sb.ToString();
    }
}
