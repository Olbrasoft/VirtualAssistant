using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtualAssistant.GitHub.Configuration;

namespace VirtualAssistant.GitHub.Services;

/// <summary>
/// Background service that automatically syncs GitHub data at configurable intervals.
/// </summary>
public class GitHubSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GitHubSyncBackgroundService> _logger;
    private readonly GitHubSettings _settings;

    private DateTime? _lastSyncTime;
    private bool _lastSyncSuccess;
    private string? _lastSyncError;
    private int _consecutiveFailures;

    /// <summary>
    /// Gets the time of the last sync attempt.
    /// </summary>
    public DateTime? LastSyncTime => _lastSyncTime;

    /// <summary>
    /// Gets whether the last sync was successful.
    /// </summary>
    public bool LastSyncSuccess => _lastSyncSuccess;

    /// <summary>
    /// Gets the error message from the last failed sync, if any.
    /// </summary>
    public string? LastSyncError => _lastSyncError;

    /// <summary>
    /// Gets the number of consecutive sync failures.
    /// </summary>
    public int ConsecutiveFailures => _consecutiveFailures;

    /// <summary>
    /// Gets whether scheduled sync is enabled.
    /// </summary>
    public bool IsEnabled => _settings.EnableScheduledSync;

    /// <summary>
    /// Gets the sync interval in minutes.
    /// </summary>
    public int SyncIntervalMinutes => _settings.SyncIntervalMinutes;

    public GitHubSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<GitHubSettings> options,
        ILogger<GitHubSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.EnableScheduledSync)
        {
            _logger.LogInformation("GitHub scheduled sync is disabled");
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.Owner))
        {
            _logger.LogWarning("GitHub Owner not configured - scheduled sync will not run");
            return;
        }

        _logger.LogInformation(
            "GitHub scheduled sync started. Interval: {Interval} minutes, Owner: {Owner}",
            _settings.SyncIntervalMinutes,
            _settings.Owner);

        // Wait a bit before first sync to let the application fully start
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await SyncAsync(stoppingToken);

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_settings.SyncIntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutdown requested
                break;
            }
        }

        _logger.LogInformation("GitHub scheduled sync stopped");
    }

    private async Task SyncAsync(CancellationToken stoppingToken)
    {
        var startTime = DateTime.UtcNow;
        _lastSyncTime = startTime;

        _logger.LogInformation("Starting scheduled GitHub sync for {Owner}", _settings.Owner);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<IGitHubSyncService>();

            var (reposSynced, issuesSynced) = await syncService.SyncAllAsync(_settings.Owner, stoppingToken);

            var duration = DateTime.UtcNow - startTime;

            _lastSyncSuccess = true;
            _lastSyncError = null;
            _consecutiveFailures = 0;

            _logger.LogInformation(
                "GitHub sync completed successfully. Repos: {Repos}, Issues: {Issues}, Duration: {Duration:F1}s",
                reposSynced,
                issuesSynced,
                duration.TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("GitHub sync cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _consecutiveFailures++;
            _lastSyncSuccess = false;
            _lastSyncError = ex.Message;

            _logger.LogError(ex,
                "GitHub sync failed (consecutive failures: {Failures})",
                _consecutiveFailures);

            // Don't crash on temporary failures - just log and continue
        }
    }

    /// <summary>
    /// Gets the current sync status for health checks.
    /// </summary>
    public GitHubSyncStatus GetStatus()
    {
        return new GitHubSyncStatus
        {
            IsEnabled = _settings.EnableScheduledSync,
            Owner = _settings.Owner,
            SyncIntervalMinutes = _settings.SyncIntervalMinutes,
            LastSyncTime = _lastSyncTime,
            LastSyncSuccess = _lastSyncSuccess,
            LastSyncError = _lastSyncError,
            ConsecutiveFailures = _consecutiveFailures,
            NextSyncTime = _lastSyncTime?.AddMinutes(_settings.SyncIntervalMinutes)
        };
    }
}

/// <summary>
/// Status information for GitHub sync health checks.
/// </summary>
public record GitHubSyncStatus
{
    public bool IsEnabled { get; init; }
    public string? Owner { get; init; }
    public int SyncIntervalMinutes { get; init; }
    public DateTime? LastSyncTime { get; init; }
    public bool LastSyncSuccess { get; init; }
    public string? LastSyncError { get; init; }
    public int ConsecutiveFailures { get; init; }
    public DateTime? NextSyncTime { get; init; }
}
