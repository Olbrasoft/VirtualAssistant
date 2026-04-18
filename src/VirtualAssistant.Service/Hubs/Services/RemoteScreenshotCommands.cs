using System.Diagnostics;
using Olbrasoft.VirtualAssistant.Service.Workers;

namespace Olbrasoft.VirtualAssistant.Service.Hubs.Services;

/// <inheritdoc />
public class RemoteScreenshotCommands : IRemoteScreenshotCommands
{
    private const string InsertScreenshotScript = "/home/jirka/.local/bin/insert-screenshot-path";
    private static string ScreenshotDir => ScreenshotWatcherWorker.ScreenshotDir;

    private readonly ILogger<RemoteScreenshotCommands> _logger;

    public RemoteScreenshotCommands(ILogger<RemoteScreenshotCommands> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<bool> IsScreenshotAvailableAsync()
    {
        try
        {
            if (!Directory.Exists(ScreenshotDir)) return Task.FromResult(false);

            var cutoff = DateTime.Now - ScreenshotWatcherWorker.FreshnessWindow;
            var hasRecent = Directory.EnumerateFiles(ScreenshotDir, "*.png")
                .Select(f => new FileInfo(f))
                .Any(fi => fi.LastWriteTime > cutoff);

            return Task.FromResult(hasRecent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "IsScreenshotAvailable check failed");
            return Task.FromResult(false);
        }
    }

    public async Task InsertScreenshotPathAsync()
    {
        _logger.LogInformation("InsertScreenshotPath");
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = InsertScreenshotScript,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(timeoutCts.Token);

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                _logger.LogWarning("InsertScreenshotPath exit code {ExitCode}: {Stderr}", process.ExitCode, stderr);
            }
            else
            {
                _logger.LogInformation("InsertScreenshotPath completed successfully");
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("InsertScreenshotPath timed out after 5s");
        }
        catch (Exception ex) { _logger.LogError(ex, "InsertScreenshotPath failed"); }
    }
}
