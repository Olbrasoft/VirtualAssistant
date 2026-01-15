using Olbrasoft.VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Workers;

/// <summary>
/// Background worker that monitors keyboard events.
/// Note: Mute toggle was removed. ScrollLock is now used for dictation (DictationWorker).
/// </summary>
public class KeyboardMonitorWorker : BackgroundService
{
    private readonly ILogger<KeyboardMonitorWorker> _logger;
    private readonly IKeyboardMonitor _keyboardMonitor;

    public KeyboardMonitorWorker(
        ILogger<KeyboardMonitorWorker> logger,
        IKeyboardMonitor keyboardMonitor)
    {
        _logger = logger;
        _keyboardMonitor = keyboardMonitor;

        // Subscribe to key events
        _keyboardMonitor.KeyReleased += OnKeyReleased;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Keyboard monitor starting...");
            await _keyboardMonitor.StartAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Keyboard monitor failed");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _keyboardMonitor.Stop();
        _keyboardMonitor.KeyReleased -= OnKeyReleased;
        return base.StopAsync(cancellationToken);
    }

    private void OnKeyReleased(object? sender, KeyEventArgs e)
    {
        _logger.LogDebug("Key released: {Key}", e.Key);
        // Note: ScrollLock is now used for dictation (DictationWorker)
        // Mute toggle via keyboard has been removed
    }
}
