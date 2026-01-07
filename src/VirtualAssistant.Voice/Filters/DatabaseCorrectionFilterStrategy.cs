using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Voice.Filters;

/// <summary>
/// Filter strategy that applies database-driven text corrections with priority ordering.
/// Corrections are cached for 5 minutes for performance.
/// </summary>
public class DatabaseCorrectionFilterStrategy : ITextFilterStrategy
{
    private readonly ILogger<DatabaseCorrectionFilterStrategy> _logger;
    private readonly ITranscriptionCorrectionRepository? _repository;

    // Database corrections cache
    private IReadOnlyList<TranscriptionCorrection> _cachedCorrections = Array.Empty<TranscriptionCorrection>();
    private DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private readonly object _lock = new();

    public DatabaseCorrectionFilterStrategy(
        ILogger<DatabaseCorrectionFilterStrategy> logger,
        ITranscriptionCorrectionRepository? repository = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository;
    }

    /// <inheritdoc/>
    public string Name => "Database Corrections";

    /// <inheritdoc/>
    public bool IsEnabled => _repository != null;

    /// <inheritdoc/>
    public string Apply(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // Refresh database corrections cache if needed
        RefreshCorrectionsCache();

        if (_cachedCorrections.Count == 0)
            return text;

        var result = text;

        // Apply corrections in priority order (highest first)
        foreach (var correction in _cachedCorrections)
        {
            var comparison = correction.CaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            if (result.Contains(correction.IncorrectText, comparison))
            {
                result = result.Replace(
                    correction.IncorrectText,
                    correction.CorrectText,
                    comparison);

                _logger.LogDebug(
                    "Applied DB correction: '{Incorrect}' → '{Correct}' (priority: {Priority})",
                    correction.IncorrectText,
                    correction.CorrectText,
                    correction.Priority);

                // Track usage asynchronously (don't block)
                _ = TrackCorrectionUsageAsync(correction.Id);
            }
        }

        return result;
    }

    /// <summary>
    /// Refreshes the database corrections cache if expired.
    /// </summary>
    private void RefreshCorrectionsCache()
    {
        if (_repository == null)
            return;

        if (DateTime.UtcNow < _cacheExpiry)
            return;

        lock (_lock)
        {
            // Double-check after acquiring lock
            if (DateTime.UtcNow < _cacheExpiry)
                return;

            try
            {
                // Synchronous call is acceptable for cache refresh
                _cachedCorrections = _repository.GetActiveCorrectionsAsync(CancellationToken.None).GetAwaiter().GetResult();

                _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);

                _logger.LogInformation(
                    "Refreshed corrections cache: {Count} active corrections (expires: {Expiry})",
                    _cachedCorrections.Count,
                    _cacheExpiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh corrections cache");

                // Retry in 30 seconds on error
                _cacheExpiry = DateTime.UtcNow.AddSeconds(30);
            }
        }
    }

    /// <summary>
    /// Tracks correction usage asynchronously for analytics.
    /// </summary>
    private async Task TrackCorrectionUsageAsync(int correctionId)
    {
        if (_repository == null)
            return;

        try
        {
            await _repository.TrackUsageAsync(correctionId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Don't fail the correction if tracking fails
            _logger.LogWarning(ex, "Failed to track correction usage for ID {CorrectionId}", correctionId);
        }
    }
}
