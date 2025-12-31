using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Voice.Configuration;
using Olbrasoft.VirtualAssistant.Voice.Similarity;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Calculates text similarity using configured algorithm and thresholds.
/// </summary>
public class SimilarityCalculator
{
    private readonly IStringSimilarity _stringSimilarity;
    private readonly TextNormalizer _textNormalizer;
    private readonly EchoDetectionOptions _options;

    public SimilarityCalculator(
        IStringSimilarity stringSimilarity,
        TextNormalizer textNormalizer,
        IOptions<EchoDetectionOptions> options)
    {
        _stringSimilarity = stringSimilarity;
        _textNormalizer = textNormalizer;
        _options = options.Value;
    }

    /// <summary>
    /// Calculates similarity between two strings.
    /// Returns value between 0.0 (no match) and 1.0 (perfect match).
    /// </summary>
    public double Calculate(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return 0.0;

        // Normalize both strings (remove diacritics for Czech language support)
        var normalizedA = _textNormalizer.RemoveDiacritics(a.ToLowerInvariant());
        var normalizedB = _textNormalizer.RemoveDiacritics(b.ToLowerInvariant());

        return _stringSimilarity.Similarity(normalizedA, normalizedB);
    }

    /// <summary>
    /// Checks if two strings are similar based on configured threshold.
    /// </summary>
    public bool IsSimilar(string a, string b)
    {
        return Calculate(a, b) >= _options.SimilarityThreshold;
    }

    /// <summary>
    /// Gets the configured similarity threshold.
    /// </summary>
    public double Threshold => _options.SimilarityThreshold;
}
