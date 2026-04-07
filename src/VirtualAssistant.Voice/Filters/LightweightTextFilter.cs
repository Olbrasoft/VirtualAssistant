using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.Filters;

/// <summary>
/// Default implementation of <see cref="ILightweightTextFilter"/>. Applies the
/// low-latency, non-LLM strategies in this order:
/// <list type="number">
///   <item>Whisper hallucination removal — drops "Konec.", "Titulky vytvořil...", etc.</item>
///   <item>Database corrections — normalizes project-specific terms like
///         "cloud kód" → "Claude Code" via <see cref="DatabaseCorrectionFilterStrategy"/>.
///         The DB cache is in-memory after warm-up so the lookup is sub-millisecond and
///         does not violate the Quick Dictation latency contract.</item>
///   <item>Whitespace normalization — trim + collapse double spaces.</item>
/// </list>
/// LLM correction is intentionally skipped — that is the difference between Quick
/// Dictation and Normal Dictation.
/// </summary>
public class LightweightTextFilter : ILightweightTextFilter
{
    private readonly WhisperHallucinationFilterStrategy _hallucinationStrategy;
    private readonly DatabaseCorrectionFilterStrategy _databaseCorrectionStrategy;
    private readonly WhitespaceFilterStrategy _whitespaceStrategy;
    private readonly ILogger<LightweightTextFilter> _logger;

    public LightweightTextFilter(
        WhisperHallucinationFilterStrategy hallucinationStrategy,
        DatabaseCorrectionFilterStrategy databaseCorrectionStrategy,
        WhitespaceFilterStrategy whitespaceStrategy,
        ILogger<LightweightTextFilter> logger)
    {
        _hallucinationStrategy = hallucinationStrategy ?? throw new ArgumentNullException(nameof(hallucinationStrategy));
        _databaseCorrectionStrategy = databaseCorrectionStrategy ?? throw new ArgumentNullException(nameof(databaseCorrectionStrategy));
        _whitespaceStrategy = whitespaceStrategy ?? throw new ArgumentNullException(nameof(whitespaceStrategy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public bool IsEnabled =>
        _hallucinationStrategy.IsEnabled
        || _databaseCorrectionStrategy.IsEnabled
        || _whitespaceStrategy.IsEnabled;

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

        // Apply database corrections (project-specific terms like "cloud kód" → "Claude Code").
        // The DB cache is loaded once and refreshed every 5 minutes; lookups are
        // sub-millisecond after warm-up.
        var afterDbCorrections = _databaseCorrectionStrategy.Apply(afterHallucination);

        var afterWhitespace = _whitespaceStrategy.Apply(afterDbCorrections);

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
