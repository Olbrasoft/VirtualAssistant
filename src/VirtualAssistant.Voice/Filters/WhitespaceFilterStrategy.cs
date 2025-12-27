using Microsoft.Extensions.Logging;

namespace Olbrasoft.VirtualAssistant.Voice.Filters;

/// <summary>
/// Filter strategy that normalizes whitespace in text.
/// Trims leading/trailing whitespace and collapses multiple spaces to single space.
/// </summary>
public class WhitespaceFilterStrategy : ITextFilterStrategy
{
    private readonly ILogger<WhitespaceFilterStrategy> _logger;

    public WhitespaceFilterStrategy(ILogger<WhitespaceFilterStrategy> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Name => "Whitespace Normalization";

    /// <inheritdoc/>
    public bool IsEnabled => true; // Always enabled

    /// <inheritdoc/>
    public string Apply(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var result = text.Trim();

        // Collapse multiple spaces to single space
        while (result.Contains("  "))
        {
            result = result.Replace("  ", " ");
        }

        if (result != text)
        {
            _logger.LogDebug("Whitespace normalized: {Before} → {After}", text.Length, result.Length);
        }

        return result;
    }
}
