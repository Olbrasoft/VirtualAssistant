using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace Olbrasoft.VirtualAssistant.Service.Services;

/// <summary>
/// Background service that plays a startup notification when the application starts.
/// Phase 1: Simple static notification "Systém nastartován".
/// </summary>
public sealed class StartupNotificationService : IHostedService
{
    private readonly TtsService _ttsService;
    private readonly ILogger<StartupNotificationService> _logger;

    public StartupNotificationService(TtsService ttsService, ILogger<StartupNotificationService> logger)
    {
        _ttsService = ttsService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Wait a moment for all services to fully initialize
        await Task.Delay(1000, cancellationToken);

        _logger.LogInformation("Playing startup notification");

        try
        {
            await _ttsService.SpeakAsync("Systém nastartován, vše v pořádku", source: null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to play startup notification");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
