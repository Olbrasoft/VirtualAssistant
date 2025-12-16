using System.Text.RegularExpressions;

namespace Olbrasoft.VirtualAssistant.Core.Utilities;

/// <summary>
/// Utility for parsing rate limit information from error messages.
/// </summary>
public static partial class RateLimitParser
{
    /// <summary>
    /// Parses reset time from rate limit error message.
    /// </summary>
    /// <param name="errorBody">Error message body</param>
    /// <returns>DateTime when rate limit resets, or null if not parseable</returns>
    public static DateTime? ParseResetTimeFromError(string errorBody)
    {
        try
        {
            // Pattern: "Please try again in Xm Y.Zs" or "Please try again in Y.Zs"
            var match = MinutesSecondsRegex().Match(errorBody);
            if (match.Success)
            {
                var minutes = int.Parse(match.Groups[1].Value);
                var seconds = double.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                return DateTime.UtcNow.AddMinutes(minutes).AddSeconds(seconds);
            }

            // Pattern: just seconds
            match = SecondsOnlyRegex().Match(errorBody);
            if (match.Success)
            {
                var seconds = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                return DateTime.UtcNow.AddSeconds(seconds);
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return null;
    }

    [GeneratedRegex(@"try again in (\d+)m([\d.]+)s")]
    private static partial Regex MinutesSecondsRegex();

    [GeneratedRegex(@"try again in ([\d.]+)s")]
    private static partial Regex SecondsOnlyRegex();
}
