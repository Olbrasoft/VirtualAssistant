using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Voice.Filters;

namespace Olbrasoft.VirtualAssistant.Voice.Services;

/// <summary>
/// Background service that pre-loads and periodically refreshes the
/// <see cref="DatabaseCorrectionFilterStrategy"/> cache so the
/// Quick Dictation hot path always finds a fresh in-memory cache and
/// never has to wait for a synchronous DB round-trip.
///
/// The refresh interval is derived from <see cref="DatabaseCorrectionFilterStrategy.CacheDuration"/>
/// with a 5-minute safety margin, so the warm-up always completes before the
/// underlying cache expires. If the strategy's TTL changes (e.g. the tray-menu
/// "Obnovit cache" button lets us relax the polling), this service follows
/// automatically.
/// </summary>
public class DatabaseCorrectionCacheWarmupService : BackgroundService
{
    private static readonly TimeSpan RefreshInterval =
        DatabaseCorrectionFilterStrategy.CacheDuration - TimeSpan.FromMinutes(5);

    private readonly DatabaseCorrectionFilterStrategy _strategy;
    private readonly ILogger<DatabaseCorrectionCacheWarmupService> _logger;

    public DatabaseCorrectionCacheWarmupService(
        DatabaseCorrectionFilterStrategy strategy,
        ILogger<DatabaseCorrectionCacheWarmupService> logger)
    {
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DatabaseCorrectionCacheWarmupService started");

        // Warm immediately on startup so the very first transcription has a
        // populated cache.
        await _strategy.WarmupAsync(stoppingToken).ConfigureAwait(false);

        // Refresh periodically — under the cache TTL — so the hot path never
        // sees an expired cache.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(RefreshInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested)
                break;

            await _strategy.WarmupAsync(stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("DatabaseCorrectionCacheWarmupService stopping");
    }
}
