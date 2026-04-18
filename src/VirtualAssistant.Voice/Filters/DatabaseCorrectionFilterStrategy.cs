using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Data.Entities;

namespace Olbrasoft.VirtualAssistant.Voice.Filters;

/// <summary>
/// Filter strategy that applies database-driven text corrections with priority ordering.
/// Corrections are cached for 1 hour for performance; use <see cref="InvalidateCache"/>
/// (wired to the tray menu) to force a reload after inserting a new row into the
/// <c>transcription_corrections</c> table.
/// </summary>
public class DatabaseCorrectionFilterStrategy : ITextFilterStrategy
{
    private readonly ILogger<DatabaseCorrectionFilterStrategy> _logger;
    private readonly ITranscriptionCorrectionRepository? _repository;

    // Database corrections cache
    private IReadOnlyList<TranscriptionCorrection> _cachedCorrections = Array.Empty<TranscriptionCorrection>();
    private DateTime _cacheExpiry = DateTime.MinValue;
    // 1 hour: with a tray-menu "Obnovit cache" button available, a short polling TTL
    // is no longer the primary path for picking up new rows. Manual invalidation is
    // instant; the periodic refresh is just a safety net. Exposed so the warm-up
    // background service can derive its refresh interval from this single source.
    public static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
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

        // Refresh database corrections cache if needed. The sync-over-async here
        // is accepted: DatabaseCorrectionCacheWarmupService pre-loads the cache
        // on startup and refreshes periodically, so this synchronous fallback
        // only fires on cold start before warmup completes, or immediately after
        // InvalidateCache() is called from the tray-menu "Obnovit cache" button —
        // and in that case the user explicitly wants a blocking refresh.
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
    /// Refreshes the database corrections cache if expired. Always acquires
    /// <see cref="_lock"/> before reading <see cref="_cacheExpiry"/>; the previous
    /// un-synchronized fast-path read could legally observe a stale expiry after
    /// <see cref="InvalidateCache"/> was called from another thread, defeating the
    /// "next dictation" guarantee. The lock is cheap in practice — Apply is only
    /// called on the dictation pipeline, not in any hot loop.
    /// </summary>
    private void RefreshCorrectionsCache()
    {
        if (_repository == null)
            return;

        lock (_lock)
        {
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
    /// Pre-loads the corrections cache asynchronously, off the request path.
    /// Called by <c>DatabaseCorrectionCacheWarmupService</c> on application startup
    /// and again on a periodic timer, so the Quick Dictation hot path always finds
    /// a fresh cache and never has to wait for a synchronous DB round-trip.
    ///
    /// Safe to call from any thread; the underlying refresh is serialized via the
    /// strategy's internal lock.
    /// </summary>
    public async Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        if (_repository == null)
            return;

        try
        {
            var corrections = await _repository.GetActiveCorrectionsAsync(cancellationToken).ConfigureAwait(false);
            lock (_lock)
            {
                _cachedCorrections = corrections;
                _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
            }
            _logger.LogInformation(
                "Warmed corrections cache: {Count} active corrections (expires: {Expiry})",
                corrections.Count,
                _cacheExpiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to warm corrections cache");
        }
    }

    /// <summary>
    /// Forces the cache to be reloaded on the next <see cref="Apply"/> call.
    /// Intended for a tray-menu action so a freshly INSERTed row in
    /// <c>transcription_corrections</c> takes effect on the very next dictation
    /// without waiting for the periodic refresh or restarting the service.
    /// Safe to call from any thread.
    /// </summary>
    public void InvalidateCache()
    {
        lock (_lock)
        {
            _cacheExpiry = DateTime.MinValue;
        }
        _logger.LogInformation("Transcription corrections cache invalidated; next Apply will reload from DB");
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
