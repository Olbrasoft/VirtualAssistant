using F23.StringSimilarity;

namespace Olbrasoft.VirtualAssistant.Voice.Similarity;

/// <summary>
/// Calculates string similarity using the Normalized Levenshtein distance algorithm.
/// </summary>
/// <remarks>
/// The Levenshtein distance is the minimum number of single-character edits 
/// (insertions, deletions, or substitutions) required to change one string into another.
/// This implementation normalizes the result to a value between 0.0 and 1.0.
/// </remarks>
public class LevenshteinSimilarity : IStringSimilarity
{
    private readonly NormalizedLevenshtein _levenshtein = new();

    /// <inheritdoc />
    public double Similarity(string a, string b)
    {
        if (a is null || b is null)
            return 0.0;
            
        return _levenshtein.Similarity(a, b);
    }
}
