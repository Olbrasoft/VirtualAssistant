using System.Globalization;
using System.Text;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Normalizes text for comparison and similarity calculations.
/// </summary>
public class TextNormalizer
{
    /// <summary>
    /// Normalizes text for comparison (lowercase, remove punctuation, trim whitespace).
    /// </summary>
    public string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Convert to lowercase and remove common punctuation
        var normalized = text.ToLowerInvariant()
            .Replace(".", "")
            .Replace(",", "")
            .Replace("!", "")
            .Replace("?", "")
            .Replace(":", "")
            .Replace(";", "")
            .Replace("\"", "")
            .Replace("'", "")
            .Replace("\u201E", "") // Czech opening quote „
            .Replace("\u201C", "") // Czech closing quote "
            .Replace("(", "")
            .Replace(")", "")
            .Replace("[", "")
            .Replace("]", "")
            .Trim();

        // Normalize whitespace
        return NormalizeWhitespace(normalized);
    }

    /// <summary>
    /// Normalizes whitespace (collapse multiple spaces to single space).
    /// </summary>
    public string NormalizeWhitespace(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var sb = new StringBuilder(text.Length);
        bool previousWasSpace = false;

        foreach (char c in text)
        {
            if (c == ' ')
            {
                if (!previousWasSpace)
                {
                    sb.Append(' ');
                    previousWasSpace = true;
                }
            }
            else
            {
                sb.Append(c);
                previousWasSpace = false;
            }
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Removes diacritics from text (e.g., "úspěšně" → "uspesne").
    /// Essential for comparing Czech text with potential ASR transcription errors.
    /// </summary>
    public string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (char c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Removes N words from the beginning of text while preserving original formatting.
    /// </summary>
    public string RemoveWordsFromBeginning(string text, int wordCount)
    {
        if (string.IsNullOrWhiteSpace(text) || wordCount <= 0)
            return text;

        // Split by whitespace but keep track of positions
        var words = new List<(int start, int end)>();
        int i = 0;

        while (i < text.Length)
        {
            // Skip whitespace
            while (i < text.Length && char.IsWhiteSpace(text[i]))
                i++;

            if (i >= text.Length)
                break;

            int wordStart = i;

            // Find end of word
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
                i++;

            words.Add((wordStart, i));
        }

        if (wordCount >= words.Count)
            return string.Empty;

        // Return text starting after the Nth word
        int cutPosition = words[wordCount].start;
        return text[cutPosition..].TrimStart();
    }
}
