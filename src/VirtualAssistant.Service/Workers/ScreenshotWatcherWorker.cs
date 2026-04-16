using Microsoft.AspNetCore.SignalR;
using Olbrasoft.VirtualAssistant.Service.Hubs;

namespace Olbrasoft.VirtualAssistant.Service.Workers;

/// <summary>
/// Watches the GNOME screenshot directory for new .png files and broadcasts
/// availability changes to Remote Control clients via DictationHub SignalR.
/// </summary>
public class ScreenshotWatcherWorker : BackgroundService
{
    private static readonly string ScreenshotDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Obrázky", "Snímky obrazovky");

    private static readonly TimeSpan FreshnessWindow = TimeSpan.FromMinutes(5);

    private readonly ILogger<ScreenshotWatcherWorker> _logger;
    private readonly IHubContext<DictationHub> _hubContext;
    private FileSystemWatcher? _watcher;
    private bool _lastBroadcastState;

    public ScreenshotWatcherWorker(
        ILogger<ScreenshotWatcherWorker> logger,
        IHubContext<DictationHub> hubContext)
    {
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Directory.Exists(ScreenshotDir))
        {
            _logger.LogWarning("Screenshot directory not found: {Dir}, watcher disabled", ScreenshotDir);
            return;
        }

        _watcher = new FileSystemWatcher(ScreenshotDir, "*.png")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        _watcher.Created += (_, e) =>
        {
            _logger.LogInformation("New screenshot detected: {File}", e.Name);
            _ = BroadcastAvailabilityAsync(true);
        };

        _logger.LogInformation("Screenshot watcher started on {Dir}", ScreenshotDir);

        // Periodic check for expiry (every 30s)
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                var available = HasRecentScreenshot();
                if (available != _lastBroadcastState)
                {
                    await BroadcastAvailabilityAsync(available);
                }
            }
            catch (OperationCanceledException) { break; }
        }
    }

    private bool HasRecentScreenshot()
    {
        try
        {
            var cutoff = DateTime.Now - FreshnessWindow;
            return Directory.EnumerateFiles(ScreenshotDir, "*.png")
                .Select(f => new FileInfo(f))
                .Any(fi => fi.LastWriteTime > cutoff);
        }
        catch { return false; }
    }

    private async Task BroadcastAvailabilityAsync(bool available)
    {
        _lastBroadcastState = available;
        try
        {
            await _hubContext.Clients.All.SendAsync("ScreenshotAvailable", available);
            _logger.LogDebug("Broadcast ScreenshotAvailable={Available}", available);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast ScreenshotAvailable");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _watcher?.Dispose();
        return base.StopAsync(cancellationToken);
    }
}
