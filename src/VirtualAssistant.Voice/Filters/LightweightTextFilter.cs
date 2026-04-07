using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.Filters;

/// <summary>
/// Default implementation of <see cref="ILightweightTextFilter"/>. Applies only the
/// configured low-latency strategies (Whisper hallucination removal + whitespace
/// normalization) — never database corrections, file replacements, or LLM correction.
/// Used in Quick Dictation to clean up Whisper artifacts before paste.
/// </summary>
public class LightweightTextFilter : ILightweightTextFilter
{
    private readonly WhisperHallucinationFilterStrategy _hallucinationStrategy;
    private readonly WhitespaceFilterStrategy _whitespaceStrategy;
    private readonly ILogger<LightweightTextFilter> _logger;

    public LightweightTextFilter(
        WhisperHallucinationFilterStrategy hallucinationStrategy,
        WhitespaceFilterStrategy whitespaceStrategy,
        ILogger<LightweightTextFilter> logger)
    {
        _hallucinationStrategy = hallucinationStrategy ?? throw new ArgumentNullException(nameof(hallucinationStrategy));
        _whitespaceStrategy = whitespaceStrategy ?? throw new ArgumentNullException(nameof(whitespaceStrategy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public bool IsEnabled => _hallucinationStrategy.IsEnabled || _whitespaceStrategy.IsEnabled;

    /// <inheritdoc/>
    public string Apply(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var afterHallucination = _hallucinationStrategy.Apply(text);

        // If the hallucination strategy wiped the text (whole-text match), there is nothing
        // left to normalize. Return early so the caller can detect the empty result and skip
        // any downstream processing such as paste/Enter.
        if (string.IsNullOrWhiteSpace(afterHallucination))
        {
            if (text != afterHallucination)
            {
                _logger.LogInformation(
                    "Lightweight filter wiped transcription as hallucination: '{Original}'",
                    text);
            }
            return string.Empty;
        }

        var afterWhitespace = _whitespaceStrategy.Apply(afterHallucination);

        if (afterWhitespace != text)
        {
            _logger.LogDebug(
                "Lightweight filter changed text: '{Before}' → '{After}'",
                text,
                afterWhitespace);
        }

        return afterWhitespace;
    }
}
