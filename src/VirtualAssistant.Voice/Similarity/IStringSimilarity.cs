namespace Olbrasoft.VirtualAssistant.Voice.Similarity;

/// <summary>
/// Defines a contract for calculating similarity between two strings.
/// </summary>
public interface IStringSimilarity
{
    /// <summary>
    /// Calculates the similarity between two strings.
    /// </summary>
    /// <param name="a">The first string.</param>
    /// <param name="b">The second string.</param>
    /// <returns>
    /// A value between 0.0 and 1.0, where:
    /// - 0.0 means the strings are completely different
    /// - 1.0 means the strings are identical
    /// </returns>
    double Similarity(string a, string b);
}
