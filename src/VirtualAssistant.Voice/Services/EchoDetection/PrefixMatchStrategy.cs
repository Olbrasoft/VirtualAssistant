using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services.EchoDetection;

/// <summary>
/// Detects echo using fuzzy prefix matching.
/// Counts consecutive matching words from the beginning and removes them if threshold is met.
/// </summary>
public class PrefixMatchStrategy : IEchoDetectionStrategy
{
    private readonly ILogger<PrefixMatchStrategy> _logger;
    private readonly TextNormalizer _textNormalizer;
    private readonly SimilarityCalculator _similarityCalculator;
    private readonly EchoDetectionOptions _options;

    public string StrategyName => "PrefixMatch";

    public PrefixMatchStrategy(
        ILogger<PrefixMatchStrategy> logger,
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

        // Count consecutive matching words from the beginning
        var consecutiveMatches = 0;
        for (int i = 0; i < textWords.Length && i < ttsWords.Length; i++)
        {
            if (_similarityCalculator.Calculate(textWords[i], ttsWords[i]) > _options.WordSimilarityThreshold)
            {
                consecutiveMatches++;
            }
            else
            {
                break; // Stop at first non-match
            }
        }

        // If we matched significant portion of TTS from the beginning, remove those words
        var ttsMatchRatio = ttsWords.Length > 0 ? (double)consecutiveMatches / ttsWords.Length : 0;

        if (consecutiveMatches >= _options.MinimumWordCount && ttsMatchRatio >= _options.TtsMatchRatioThreshold)
        {
            _logger.LogDebug("[{StrategyName}] Fuzzy prefix match! {ConsecutiveMatches} consecutive words match TTS ({TtsMatchRatio:P0} of TTS)",
                StrategyName, consecutiveMatches, ttsMatchRatio);

            var remainingText = _textNormalizer.RemoveWordsFromBeginning(transcription, consecutiveMatches);
            return (true, remainingText, ttsMatchRatio);
        }

        return (false, transcription, ttsMatchRatio);
    }
}
