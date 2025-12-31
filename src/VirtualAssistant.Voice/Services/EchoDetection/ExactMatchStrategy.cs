using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Voice.Configuration;

namespace Olbrasoft.VirtualAssistant.Voice.Services.EchoDetection;

/// <summary>
/// Detects echo using exact substring matching.
/// Checks if the entire transcription is contained within the TTS message.
/// This handles cases where Whisper starts recording mid-sentence.
/// </summary>
public class ExactMatchStrategy : IEchoDetectionStrategy
{
    private readonly ILogger<ExactMatchStrategy> _logger;
    private readonly TextNormalizer _textNormalizer;
    private readonly EchoDetectionOptions _options;

    public string StrategyName => "ExactMatch";

    public ExactMatchStrategy(
        ILogger<ExactMatchStrategy> logger,
        TextNormalizer textNormalizer,
        IOptions<EchoDetectionOptions> options)
    {
        _logger = logger;
        _textNormalizer = textNormalizer;
        _options = options.Value;
    }

    public (bool wasRemoved, string remainingText, double similarity) DetectAndRemove(string transcription, string ttsMessage)
    {
        if (string.IsNullOrWhiteSpace(transcription) || string.IsNullOrWhiteSpace(ttsMessage))
            return (false, transcription, 0.0);

        var textNormalized = _textNormalizer.Normalize(transcription);
        var ttsNormalized = _textNormalizer.Normalize(ttsMessage);

        var textWords = textNormalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (textWords.Length < _options.MinimumWordCount)
            return (false, transcription, 0.0);

        // Check if text is a SUBSTRING of TTS (Whisper captured only part of TTS)
        var textNormalizedForContains = _textNormalizer.RemoveDiacritics(textNormalized.ToLowerInvariant());
        var ttsNormalizedForContains = _textNormalizer.RemoveDiacritics(ttsNormalized.ToLowerInvariant());

        if (ttsNormalizedForContains.Contains(textNormalizedForContains))
        {
            _logger.LogDebug("[{StrategyName}] Substring match! Text is contained in TTS message", StrategyName);
            // The entire captured text is part of TTS output - it's an echo
            return (true, string.Empty, 1.0);
        }

        return (false, transcription, 0.0);
    }
}
