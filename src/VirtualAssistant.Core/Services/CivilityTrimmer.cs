using System.Text.RegularExpressions;

namespace Olbrasoft.VirtualAssistant.Core.Services;

/// <summary>
/// Strips trailing polite/filler sentences (e.g. "Děkuji.", "Ahoj.") from a
/// dictated transcript when it is being pasted into a CLI agent (Claude Code
/// etc.). Two sources feed this: the user's habit of saying "Děkuji." after a
/// request, and Whisper hallucinating subtitle-style sign-offs during silent
/// tails of audio (its training corpus is heavy on TED/subtitle tracks).
/// </summary>
/// <remarks>
/// Applied only on the quick-dictation path into known CLI agents — the LLM
/// correction pipeline is bypassed there, so without this pass the agent
/// prompt ends up with "…do X. Děkuji." which is noise. Regular dictation into
/// chat apps (WhatsApp etc.) must NOT invoke this; "Děkuji." is a legitimate
/// message there.
/// </remarks>
public static class CivilityTrimmer
{
    // Whole-sentence civility phrases, normalized to lowercase and without
    // trailing punctuation. Comparisons are case-insensitive so "Děkuji" and
    // "děkuji" both match. Substring matches never trigger — only a full
    // trailing sentence is eligible — so "díky tomu, že…" mid-text is safe.
    private static readonly HashSet<string> CivilityPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        "děkuji",
        "děkuju",
        "děkujeme",
        "díky",
        "díky moc",
        "díky mockrát",
        "díky za info",
        "díky za informaci",
        "ahoj",
        "nashle",
        "nashledanou",
        "čau",
        "měj se",
        "měj se hezky",
        // Whisper hallucinations during silent audio tails — the model was
        // trained on tons of Czech subtitle tracks ending with these sign-offs.
        "děkuji za titulky",
        "děkuji za sledování",
        "děkuji za pozornost",
    };

    // Match the final sentence: anchored at the end, starts either at the
    // string start or immediately after a previous sentence terminator. The
    // inner group is the sentence content (no terminators), non-greedy so the
    // match stays confined to the last sentence.
    private static readonly Regex TrailingSentence = new(
        @"(?:^|(?<=[.!?]))\s*([^.!?]+?)\s*[.!?]+\s*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Returns <paramref name="text"/> with any trailing civility-only
    /// sentences stripped. Repeats the strip so multiple tacked-on civilities
    /// ("… Děkuji. Ahoj.") all come off. Null is normalized to empty.
    /// When nothing is stripped the original text is returned verbatim —
    /// trailing whitespace/newlines the user typed are preserved, never
    /// silently normalized by this pass.
    /// </summary>
    public static string StripTrailingCivility(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text ?? string.Empty;
        }

        var current = text;
        while (true)
        {
            var trimmedEnd = current.TrimEnd();
            if (trimmedEnd.Length == 0)
            {
                return string.Empty;
            }

            var match = TrailingSentence.Match(trimmedEnd);
            if (!match.Success)
            {
                return current;
            }

            var phrase = match.Groups[1].Value.Trim();
            if (!CivilityPhrases.Contains(phrase))
            {
                return current;
            }

            // Keep only the content before the matched civility sentence —
            // this also discards the whitespace/newlines between the last
            // real sentence and the stripped trailer, which is the desired
            // behaviour when a strip actually happens.
            current = trimmedEnd[..match.Index];
        }
    }
}
