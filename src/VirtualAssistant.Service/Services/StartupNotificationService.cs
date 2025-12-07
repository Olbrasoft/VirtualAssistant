using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VirtualAssistant.Core.Services;

namespace Olbrasoft.VirtualAssistant.Service.Services;

/// <summary>
/// Background service that plays a startup notification when the application starts.
/// Phase 1: Simple static notification "Systém nastartován".
/// </summary>
public sealed class StartupNotificationService : IHostedService
{
    private readonly IVirtualAssistantSpeaker _speaker;
    private readonly ILogger<StartupNotificationService> _logger;

    public StartupNotificationService(IVirtualAssistantSpeaker speaker, ILogger<StartupNotificationService> logger)
    {
        _speaker = speaker;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Wait a moment for all services to fully initialize
        await Task.Delay(1000, cancellationToken);

        _logger.LogInformation("Playing startup notification");

        try
        {
            // No agent name = always speak (startup notification)
            await _speaker.SpeakAsync("Systém nastartován, vše v pořádku", agentName: null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to play startup notification");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
