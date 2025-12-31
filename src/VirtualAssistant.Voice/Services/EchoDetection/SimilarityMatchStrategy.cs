using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services.EchoDetection;

/// <summary>
/// Detects echo using similarity-based prefix matching.
/// Tries different prefix lengths and finds the best match above threshold.
/// </summary>
public class SimilarityMatchStrategy : IEchoDetectionStrategy
{
    private readonly ILogger<SimilarityMatchStrategy> _logger;
    private readonly TextNormalizer _textNormalizer;
    private readonly SimilarityCalculator _similarityCalculator;
    private readonly EchoDetectionOptions _options;

    public string StrategyName => "SimilarityMatch";

    public SimilarityMatchStrategy(
        ILogger<SimilarityMatchStrategy> logger,
        TextNormalizer textNormalizer,
        SimilarityCalculator similarityCalculator,
        IOptions<EchoDetectionOptions> options)
    {
        _logger = logger;
        _textNormalizer = textNormalizer;
        _similarityCalculator = similarityCalculator;
        _options = options.Value;
    }

    public (bool wasRemoved, string remainingText, double similarity) DetectAndRemove(string transcription, string ttsMessage)
    {
        if (string.IsNullOrWhiteSpace(transcription) || string.IsNullOrWhiteSpace(ttsMessage))
            return (false, transcription, 0.0);

        var textNormalized = _textNormalizer.Normalize(transcription);
        var ttsNormalized = _textNormalizer.Normalize(ttsMessage);

        var textWords = textNormalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var ttsWords = ttsNormalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (textWords.Length == 0 || ttsWords.Length == 0)
            return (false, transcription, 0.0);

        // Take prefix from text with same word count as TTS (+/- some tolerance)
        // We allow some tolerance because Whisper might add/remove words
        var minPrefixLen = Math.Max(1, ttsWords.Length - 2);
        var maxPrefixLen = Math.Min(textWords.Length, ttsWords.Length + 2);

        double bestSimilarity = 0;
        int bestPrefixLength = 0;

        // Try different prefix lengths and find best match
        for (int prefixLen = minPrefixLen; prefixLen <= maxPrefixLen; prefixLen++)
        {
            var prefix = string.Join(" ", textWords.Take(prefixLen));
            var similarity = _similarityCalculator.Calculate(prefix, ttsNormalized);

            _logger.LogDebug("[{StrategyName}] prefixLen={PrefixLen}, similarity={Similarity:P1}, prefix=\"{Prefix}\"",
                StrategyName, prefixLen, similarity, prefix);

            if (similarity > bestSimilarity)
            {
                bestSimilarity = similarity;
                bestPrefixLength = prefixLen;
            }
        }

        _logger.LogDebug("[{StrategyName}] Best: prefixLen={BestPrefixLength}, similarity={BestSimilarity:P1}, threshold={Threshold:P0}, TTS normalized: \"{TtsNormalized}\"",
            StrategyName, bestPrefixLength, bestSimilarity, _options.SimilarityThreshold, ttsNormalized);

        if (bestSimilarity >= _options.SimilarityThreshold)
        {
            // Remove the prefix from original text
            var remainingText = _textNormalizer.RemoveWordsFromBeginning(transcription, bestPrefixLength);
            return (true, remainingText, bestSimilarity);
        }

        return (false, transcription, bestSimilarity);
    }
}
