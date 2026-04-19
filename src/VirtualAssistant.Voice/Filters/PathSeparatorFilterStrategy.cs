using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.Filters;

/// <summary>
/// Replaces the dictated Czech word <c>lomeno</c> ("slash") with the
/// literal <c>/</c> character and collapses whitespace on either side
/// so adjacent words join cleanly into a path-like form. A literal DB
/// substitution (<c>"lomeno" → "/"</c>) can't do this — it leaves the
/// spaces in place (<c>"Dokumenty / přístupy"</c>). This strategy runs
/// in the lightweight pipeline so it fires on Quick Dictation too,
/// where no LLM is in the loop.
/// <para>
/// Transformations:
/// <list type="bullet">
///   <item><c>"Dokumenty lomeno přístupy"</c> → <c>"Dokumenty/přístupy"</c></item>
///   <item><c>"a lomeno b lomeno c"</c> → <c>"a/b/c"</c> (chained)</item>
///   <item><c>"Lomeno začátek"</c> → <c>"/začátek"</c> (leading)</item>
///   <item><c>"konec lomeno"</c> → <c>"konec/"</c> (trailing)</item>
/// </list>
/// Unicode word boundaries mean <c>\b</c> correctly separates Czech
/// words like <c>"přístupy"</c> from <c>"lomeno"</c>.
/// </para>
/// </summary>
public partial class PathSeparatorFilterStrategy : ITextFilterStrategy
{
    // Matches the word "lomeno" as a whole token, greedily consuming any
    // whitespace on either side so the replacement collapses surrounding
    // spaces atomically. Case-insensitive to also catch "Lomeno" at the
    // start of a Whisper-capitalized segment.
    [GeneratedRegex(@"\s*\blomeno\b\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LomenoPattern();

    private readonly ILogger<PathSeparatorFilterStrategy> _logger;

    public PathSeparatorFilterStrategy(ILogger<PathSeparatorFilterStrategy> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Name => "Path Separator (lomeno → /)";

    /// <inheritdoc/>
    public bool IsEnabled => true;

    /// <inheritdoc/>
    public string Apply(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var result = LomenoPattern().Replace(text, "/");

        if (result != text)
        {
            _logger.LogDebug("Path separator applied: '{Before}' → '{After}'", text, result);
        }

        return result;
    }
}
