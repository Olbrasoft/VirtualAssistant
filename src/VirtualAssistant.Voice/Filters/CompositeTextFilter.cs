using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.Filters;

/// <summary>
/// Composite filter that applies multiple text filtering strategies in sequence.
/// Implements Composite pattern for combining multiple filters.
/// </summary>
public class CompositeTextFilter : ITextFilter
{
    private readonly IEnumerable<ITextFilterStrategy> _strategies;
    private readonly ILogger<CompositeTextFilter> _logger;

    public CompositeTextFilter(
        IEnumerable<ITextFilterStrategy> strategies,
        ILogger<CompositeTextFilter> logger)
    {
        _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public bool IsEnabled => _strategies.Any(s => s.IsEnabled);

    /// <inheritdoc/>
    public string Apply(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var result = text;
        var original = text;

        foreach (var strategy in _strategies.Where(s => s.IsEnabled))
        {
            var before = result;
            result = strategy.Apply(result);

            if (before != result)
            {
                _logger.LogDebug(
                    "Filter '{FilterName}' applied: '{Before}' → '{After}'",
                    strategy.Name,
                    before,
                    result);
            }
        }

        if (result != original)
        {
            _logger.LogInformation(
                "Applied {Count} filters: {OriginalLength} → {FilteredLength} chars",
                _strategies.Count(s => s.IsEnabled),
                original.Length,
                result.Length);
        }

        return result;
    }
}
