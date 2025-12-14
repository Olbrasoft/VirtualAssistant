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

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // TTS disabled - notifications are stored in database, not spoken immediately
        _logger.LogInformation("Startup notification service ready (TTS disabled)");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
