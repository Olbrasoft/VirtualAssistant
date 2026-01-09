using Microsoft.Extensions.Options;
using Olbrasoft.VirtualAssistant.Service.Configuration;
using Olbrasoft.VirtualAssistant.Service.Services;
using Olbrasoft.VirtualAssistant.Service.Tray.Menu;

namespace Olbrasoft.VirtualAssistant.Service.Workers;

/// <summary>
/// Background worker that periodically checks if LLM prompts are out of sync.
/// Updates menu state when source prompts are newer than deployed prompts.
/// </summary>
public class PromptSyncCheckWorker : BackgroundService
{
    private readonly ILogger<PromptSyncCheckWorker> _logger;
    private readonly IPromptSyncService _promptSyncService;
    private readonly IMenuStateManager _menuStateManager;
    private readonly TimeSpan _checkInterval;

    public PromptSyncCheckWorker(
        ILogger<PromptSyncCheckWorker> logger,
        IPromptSyncService promptSyncService,
        IMenuStateManager menuStateManager,
        IOptions<PromptSyncOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _promptSyncService = promptSyncService ?? throw new ArgumentNullException(nameof(promptSyncService));
        _menuStateManager = menuStateManager ?? throw new ArgumentNullException(nameof(menuStateManager));

        var intervalSeconds = options?.Value.CheckIntervalSeconds ?? 30;
        _checkInterval = TimeSpan.FromSeconds(intervalSeconds > 0 ? intervalSeconds : 30);

        _logger.LogInformation("PromptSyncCheckWorker initialized with interval: {Interval}s", _checkInterval.TotalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay to let the application start up
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        // Initial check
        CheckPromptSync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
                CheckPromptSync();
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in prompt sync check loop");
            }
        }

        _logger.LogInformation("PromptSyncCheckWorker stopped");
    }

    private void CheckPromptSync()
    {
        try
        {
            // Skip if currently syncing or already failed
            var currentStatus = _menuStateManager.PromptSyncStatus;
            if (currentStatus == PromptSyncStatus.Syncing)
            {
                return;
            }

            var isOutOfSync = _promptSyncService.ArePromptsOutOfSync();

            if (isOutOfSync && currentStatus != PromptSyncStatus.OutOfSync)
            {
                _logger.LogInformation("Prompts are out of sync - source files have changed");
                _menuStateManager.UpdatePromptSyncStatus(PromptSyncStatus.OutOfSync);
            }
            else if (!isOutOfSync && currentStatus == PromptSyncStatus.OutOfSync)
            {
                // This shouldn't happen normally, but handle it gracefully
                _logger.LogDebug("Prompts are now in sync");
                _menuStateManager.UpdatePromptSyncStatus(PromptSyncStatus.InSync);
            }
            else if (currentStatus == PromptSyncStatus.Unknown && !isOutOfSync)
            {
                // Initial state - prompts are in sync
                _menuStateManager.UpdatePromptSyncStatus(PromptSyncStatus.InSync);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking prompt sync status");
        }
    }
}
