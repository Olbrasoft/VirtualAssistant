using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Core.Services;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Service.Workers;

/// <summary>
/// Background worker that monitors keyboard for ScrollLock key to toggle mute.
/// </summary>
public class KeyboardMonitorWorker : BackgroundService
{
    private readonly ILogger<KeyboardMonitorWorker> _logger;
    private readonly IKeyboardMonitor _keyboardMonitor;
    private readonly IManualMuteService _muteService;

    public KeyboardMonitorWorker(
        ILogger<KeyboardMonitorWorker> logger,
        IKeyboardMonitor keyboardMonitor,
        IManualMuteService muteService)
    {
        _logger = logger;
        _keyboardMonitor = keyboardMonitor;
        _muteService = muteService;

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
        _logger.LogInformation("Key released: {Key}", e.Key);
        
        if (e.Key == KeyCode.ScrollLock)
        {
            _logger.LogInformation("ScrollLock released - toggling mute");
            _muteService.Toggle();
        }
    }
}
